package com.example.infoapp

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : AppCompatActivity() {

    private val PERMISSIONS = arrayOf(
        Manifest.permission.READ_CONTACTS,
        Manifest.permission.READ_CALL_LOG
    )
    private val PERMISSION_REQUEST_CODE = 100

    private lateinit var statusText: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        statusText = findViewById(R.id.statusText)
        val syncButton: Button = findViewById(R.id.syncButton)

        syncButton.setOnClickListener {
            if (checkPermissions()) {
                startServer()
            } else {
                requestPermissions()
            }
        }

        // Auto-start server
        if (checkPermissions()) {
            startServer()
        } else {
            requestPermissions()
        }
    }

    private fun checkPermissions(): Boolean {
        return PERMISSIONS.all {
            ContextCompat.checkSelfPermission(this, it) == PackageManager.PERMISSION_GRANTED
        }
    }

    private fun requestPermissions() {
        ActivityCompat.requestPermissions(this, PERMISSIONS, PERMISSION_REQUEST_CODE)
    }

    private var server: io.grpc.Server? = null

    private fun startServer() {
        if (server != null && !server!!.isShutdown) {
            Toast.makeText(this, "Server already running", Toast.LENGTH_SHORT).show()
            return
        }

        statusText.text = "Starting Server..."
        
        CoroutineScope(Dispatchers.IO).launch {
            try {
                // Use NettyServerBuilder explicitly and bind to localhost (127.0.0.1)
                // Changing port to 50053 to avoid potential "Address in use" issues on 50052
                server = io.grpc.netty.shaded.io.grpc.netty.NettyServerBuilder
                    .forAddress(java.net.InetSocketAddress("127.0.0.1", 50053))
                    .addService(InfoProviderService(applicationContext))
                    .build()
                    .start()

                withContext(Dispatchers.Main) {
                    statusText.text = "Server listening on port 50053\nWaiting for PC to connect..."
                    Toast.makeText(this@MainActivity, "Server Started", Toast.LENGTH_SHORT).show()
                }
                
                // Keep the server running
                server?.awaitTermination()

            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    statusText.text = "Server Error: ${e.toString()}"
                    e.printStackTrace()
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        server?.shutdown()
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == PERMISSION_REQUEST_CODE) {
            if (grantResults.isNotEmpty() && grantResults.all { it == PackageManager.PERMISSION_GRANTED }) {
                startServer()
            } else {
                Toast.makeText(this, "Permissions required", Toast.LENGTH_SHORT).show()
            }
        }
    }
}
