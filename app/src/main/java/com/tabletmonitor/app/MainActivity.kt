package com.tabletmonitor.app

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast

class MainActivity : Activity() {

    private val preferences by lazy {
        getSharedPreferences("tablet_monitor", MODE_PRIVATE)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val padding = dp(24)
        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setPadding(padding, padding, padding, padding)
            setBackgroundColor(Color.rgb(18, 18, 18))
        }

        val title = TextView(this).apply {
            text = "TabletMonitor"
            textSize = 30f
            setTextColor(Color.WHITE)
            gravity = Gravity.CENTER
        }

        val description = TextView(this).apply {
            text = "Conecte ao servidor de vídeo do computador"
            textSize = 16f
            setTextColor(Color.LTGRAY)
            gravity = Gravity.CENTER
            setPadding(0, dp(8), 0, dp(24))
        }

        val hostInput = EditText(this).apply {
            hint = "IP do computador"
            setText(preferences.getString(KEY_HOST, "172.20.200.200"))
            inputType = InputType.TYPE_CLASS_TEXT
            setSingleLine(true)
            setTextColor(Color.WHITE)
            setHintTextColor(Color.GRAY)
        }

        val portInput = EditText(this).apply {
            hint = "Porta"
            setText(preferences.getInt(KEY_PORT, DEFAULT_PORT).toString())
            inputType = InputType.TYPE_CLASS_NUMBER
            setSingleLine(true)
            setTextColor(Color.WHITE)
            setHintTextColor(Color.GRAY)
        }

        val connectButton = Button(this).apply {
            text = "Conectar"
            setOnClickListener {
                connect(hostInput.text.toString(), portInput.text.toString())
            }
        }

        val matchWidth = LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        )

        content.addView(title, matchWidth)
        content.addView(description, matchWidth)
        content.addView(hostInput, matchWidth)
        content.addView(portInput, matchWidth)
        content.addView(connectButton, matchWidth)

        setContentView(content)
    }

    private fun connect(rawHost: String, rawPort: String) {
        val host = rawHost.trim()
        val port = rawPort.toIntOrNull()

        if (host.isEmpty()) {
            Toast.makeText(this, "Informe o IP do computador", Toast.LENGTH_SHORT).show()
            return
        }

        if (port == null || port !in 1..65535) {
            Toast.makeText(this, "Informe uma porta válida", Toast.LENGTH_SHORT).show()
            return
        }

        preferences.edit()
            .putString(KEY_HOST, host)
            .putInt(KEY_PORT, port)
            .apply()

        startActivity(
            Intent(this, MonitorActivity::class.java)
                .putExtra(MonitorActivity.EXTRA_HOST, host)
                .putExtra(MonitorActivity.EXTRA_PORT, port)
        )
    }

    private fun dp(value: Int): Int {
        return (value * resources.displayMetrics.density).toInt()
    }

    companion object {
        private const val KEY_HOST = "host"
        private const val KEY_PORT = "port"
        private const val DEFAULT_PORT = 5000
    }
}

