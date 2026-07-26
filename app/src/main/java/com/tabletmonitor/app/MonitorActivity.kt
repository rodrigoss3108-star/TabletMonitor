package com.tabletmonitor.app

import android.app.Activity
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.SurfaceHolder
import android.view.SurfaceView
import android.view.View
import android.view.WindowInsets
import android.view.WindowInsetsController
import android.view.WindowManager
import android.widget.FrameLayout
import android.widget.TextView

class MonitorActivity : Activity(), SurfaceHolder.Callback {

    private lateinit var surfaceView: SurfaceView
    private lateinit var statusView: TextView
    private var streamClient: H264StreamClient? = null
    private var host: String = ""
    private var port: Int = 0

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        host = intent.getStringExtra(EXTRA_HOST).orEmpty()
        port = intent.getIntExtra(EXTRA_PORT, DEFAULT_PORT)

        if (host.isBlank()) {
            finish()
            return
        }

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        surfaceView = SurfaceView(this).apply {
            holder.addCallback(this@MonitorActivity)
        }

        statusView = TextView(this).apply {
            text = "Preparando conexão..."
            textSize = 16f
            setTextColor(Color.WHITE)
            setBackgroundColor(Color.argb(170, 0, 0, 0))
            gravity = Gravity.CENTER
            setPadding(dp(18), dp(10), dp(18), dp(10))
        }

        val root = FrameLayout(this).apply {
            setBackgroundColor(Color.BLACK)
            addView(
                surfaceView,
                FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.MATCH_PARENT,
                    FrameLayout.LayoutParams.MATCH_PARENT
                )
            )
            addView(
                statusView,
                FrameLayout.LayoutParams(
                    FrameLayout.LayoutParams.WRAP_CONTENT,
                    FrameLayout.LayoutParams.WRAP_CONTENT,
                    Gravity.TOP or Gravity.CENTER_HORIZONTAL
                ).apply {
                    topMargin = dp(20)
                }
            )
        }

        setContentView(root)
        root.post(::enterFullscreen)
    }

    override fun surfaceCreated(holder: SurfaceHolder) {
        streamClient = H264StreamClient(
            host = host,
            port = port,
            surface = holder.surface,
            onStatus = ::showStatus
        ).also {
            it.start()
        }
    }

    override fun surfaceChanged(
        holder: SurfaceHolder,
        format: Int,
        width: Int,
        height: Int
    ) = Unit

    override fun surfaceDestroyed(holder: SurfaceHolder) {
        streamClient?.stop()
        streamClient = null
    }

    override fun onDestroy() {
        streamClient?.stop()
        streamClient = null
        super.onDestroy()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            enterFullscreen()
        }
    }

    private fun showStatus(message: String, connected: Boolean) {
        runOnUiThread {
            statusView.text = message
            statusView.visibility = View.VISIBLE

            if (connected) {
                statusView.postDelayed(
                    { statusView.visibility = View.GONE },
                    STATUS_HIDE_DELAY_MS
                )
            }
        }
    }

    private fun enterFullscreen() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            window.setDecorFitsSystemWindows(false)
            window.insetsController?.apply {
                hide(WindowInsets.Type.statusBars() or WindowInsets.Type.navigationBars())
                systemBarsBehavior =
                    WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            }
            return
        }

        @Suppress("DEPRECATION")
        window.decorView.systemUiVisibility = (
            View.SYSTEM_UI_FLAG_FULLSCREEN
                or View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                or View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                or View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                or View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            )
    }

    private fun dp(value: Int): Int {
        return (value * resources.displayMetrics.density).toInt()
    }

    companion object {
        const val EXTRA_HOST = "host"
        const val EXTRA_PORT = "port"

        private const val DEFAULT_PORT = 5000
        private const val STATUS_HIDE_DELAY_MS = 2_000L
    }
}
