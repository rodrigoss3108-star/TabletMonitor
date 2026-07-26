package com.tabletmonitor.app

import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.os.Build
import android.view.Surface
import java.io.DataInputStream
import java.io.EOFException
import java.net.InetSocketAddress
import java.net.Socket
import java.net.SocketException
import java.nio.charset.StandardCharsets

class H264StreamClient(
    private val host: String,
    private val port: Int,
    private val surface: Surface,
    private val onStatus: (message: String, connected: Boolean) -> Unit
) {

    @Volatile
    private var running = false

    @Volatile
    private var activeSocket: Socket? = null

    private var worker: Thread? = null

    fun start() {
        if (running) {
            return
        }

        running = true
        worker = Thread(::connectionLoop, "TabletMonitor-H264").apply {
            start()
        }
    }

    fun stop() {
        running = false
        activeSocket?.close()
        activeSocket = null
        worker?.interrupt()
        worker = null
    }

    private fun connectionLoop() {
        while (running) {
            try {
                runSession()
            } catch (_: InterruptedException) {
                Thread.currentThread().interrupt()
                return
            } catch (error: Exception) {
                if (running) {
                    onStatus(
                        "Sem conexão: ${error.message ?: error.javaClass.simpleName}. " +
                            "Nova tentativa em 2 segundos...",
                        false
                    )
                    Thread.sleep(RECONNECT_DELAY_MS)
                }
            }
        }
    }

    private fun runSession() {
        var codec: MediaCodec? = null

        try {
            Socket().use { socket ->
                activeSocket = socket
                socket.tcpNoDelay = true
                socket.receiveBufferSize = SOCKET_BUFFER_SIZE

                onStatus("Conectando a $host:$port...", false)
                socket.connect(InetSocketAddress(host, port), CONNECT_TIMEOUT_MS)

                DataInputStream(socket.getInputStream().buffered()).use { input ->
                    val streamInfo = readStreamHeader(input)
                    codec = createDecoder(streamInfo)

                    onStatus(
                        "Conectado — ${streamInfo.width} × ${streamInfo.height} " +
                            "${streamInfo.framesPerSecond} FPS",
                        true
                    )

                    decodeFrames(
                        input = input,
                        decoder = codec,
                        framesPerSecond = streamInfo.framesPerSecond
                    )
                }
            }
        } catch (error: SocketException) {
            if (running) {
                throw error
            }
        } finally {
            activeSocket = null
            releaseDecoder(codec)
        }
    }

    private fun readStreamHeader(input: DataInputStream): StreamInfo {
        val magicBytes = ByteArray(PROTOCOL_MAGIC.length)
        input.readFully(magicBytes)

        val magic = String(magicBytes, StandardCharsets.US_ASCII)
        require(magic == PROTOCOL_MAGIC) {
            "Servidor incompatível: assinatura $magic"
        }

        val version = input.readUnsignedByte()
        require(version == PROTOCOL_VERSION) {
            "Versão de protocolo incompatível: $version"
        }

        val width = input.readUnsignedShort()
        val height = input.readUnsignedShort()
        val framesPerSecond = input.readUnsignedByte()

        require(width in MIN_VIDEO_SIZE..MAX_VIDEO_WIDTH) {
            "Largura inválida: $width"
        }
        require(height in MIN_VIDEO_SIZE..MAX_VIDEO_HEIGHT) {
            "Altura inválida: $height"
        }
        require(framesPerSecond in 1..MAX_FRAMES_PER_SECOND) {
            "FPS inválido: $framesPerSecond"
        }

        return StreamInfo(width, height, framesPerSecond)
    }

    private fun createDecoder(streamInfo: StreamInfo): MediaCodec {
        val format = MediaFormat.createVideoFormat(
            MediaFormat.MIMETYPE_VIDEO_AVC,
            streamInfo.width,
            streamInfo.height
        ).apply {
            setInteger(MediaFormat.KEY_FRAME_RATE, streamInfo.framesPerSecond)
            setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, MAX_FRAME_SIZE)
        }

        val decoder = MediaCodec.createDecoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            val capabilities = decoder.codecInfo.getCapabilitiesForType(
                MediaFormat.MIMETYPE_VIDEO_AVC
            )
            if (
                capabilities.isFeatureSupported(
                    MediaCodecInfo.CodecCapabilities.FEATURE_LowLatency
                )
            ) {
                format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1)
            }
        }

        decoder.configure(format, surface, null, 0)
        decoder.start()
        return decoder
    }

    private fun decodeFrames(
        input: DataInputStream,
        decoder: MediaCodec?,
        framesPerSecond: Int
    ) {
        requireNotNull(decoder)

        val bufferInfo = MediaCodec.BufferInfo()
        var frameNumber = 0L

        while (running) {
            val frameSize = try {
                input.readInt()
            } catch (_: EOFException) {
                throw EOFException("O servidor encerrou a transmissão")
            }

            require(frameSize in 1..MAX_FRAME_SIZE) {
                "Tamanho de frame inválido: $frameSize"
            }

            val frame = ByteArray(frameSize)
            input.readFully(frame)

            queueFrame(
                decoder = decoder,
                frame = frame,
                presentationTimeUs =
                    frameNumber * MICROSECONDS_PER_SECOND / framesPerSecond
            )
            drainDecoder(decoder, bufferInfo)
            frameNumber++
        }
    }

    private fun queueFrame(
        decoder: MediaCodec,
        frame: ByteArray,
        presentationTimeUs: Long
    ) {
        while (running) {
            val inputIndex = decoder.dequeueInputBuffer(CODEC_TIMEOUT_US)

            if (inputIndex >= 0) {
                val inputBuffer = requireNotNull(decoder.getInputBuffer(inputIndex))
                require(frame.size <= inputBuffer.capacity()) {
                    "Frame maior que o buffer do decodificador"
                }

                inputBuffer.clear()
                inputBuffer.put(frame)
                decoder.queueInputBuffer(
                    inputIndex,
                    0,
                    frame.size,
                    presentationTimeUs,
                    0
                )
                return
            }
        }
    }

    private fun drainDecoder(
        decoder: MediaCodec,
        bufferInfo: MediaCodec.BufferInfo
    ) {
        var outputIndex = decoder.dequeueOutputBuffer(bufferInfo, 0)

        while (outputIndex >= 0) {
            decoder.releaseOutputBuffer(outputIndex, true)
            outputIndex = decoder.dequeueOutputBuffer(bufferInfo, 0)
        }
    }

    private fun releaseDecoder(decoder: MediaCodec?) {
        if (decoder == null) {
            return
        }

        runCatching {
            decoder.stop()
        }
        runCatching {
            decoder.release()
        }
    }

    private data class StreamInfo(
        val width: Int,
        val height: Int,
        val framesPerSecond: Int
    )

    companion object {
        private const val PROTOCOL_MAGIC = "TMON"
        private const val PROTOCOL_VERSION = 1

        private const val CONNECT_TIMEOUT_MS = 5_000
        private const val RECONNECT_DELAY_MS = 2_000L
        private const val CODEC_TIMEOUT_US = 10_000L
        private const val MICROSECONDS_PER_SECOND = 1_000_000L

        private const val SOCKET_BUFFER_SIZE = 4 * 1024 * 1024
        private const val MAX_FRAME_SIZE = 8 * 1024 * 1024
        private const val MIN_VIDEO_SIZE = 240
        private const val MAX_VIDEO_WIDTH = 7680
        private const val MAX_VIDEO_HEIGHT = 4320
        private const val MAX_FRAMES_PER_SECOND = 120
    }
}

