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
import com.example.infoapp.proto.CallLogList
import com.example.infoapp.proto.ContactList
import com.example.infoapp.proto.DataTransferServiceGrpcKt
import com.example.infoapp.utils.ContentReader
import io.grpc.ManagedChannelBuilder
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
                startSync()
            } else {
                requestPermissions()
            }
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

    private fun startSync() {
        statusText.text = "Syncing..."
        val reader = ContentReader(this)
        
        CoroutineScope(Dispatchers.IO).launch {
            try {
                val contacts = reader.getContacts()
                val callLogs = reader.getCallLogs()

                // Connect to PC via adb reverse tcp:50051 tcp:50051
                // Use 10.0.2.2 for Emulator, or localhost if using adb reverse
                // Since we plan to use adb reverse, localhost should work on the device side mapping to host
                val channel = ManagedChannelBuilder.forAddress("localhost", 50052)
                    .usePlaintext()
                    .build()

                val stub = DataTransferServiceGrpcKt.DataTransferServiceCoroutineStub(channel)

                val contactList = ContactList.newBuilder().addAllContacts(contacts).build()
                val callLogList = CallLogList.newBuilder().addAllLogs(callLogs).build()

                val contactResponse = stub.sendContacts(contactList)
                val logResponse = stub.sendCallLogs(callLogList)

                withContext(Dispatchers.Main) {
                    statusText.text = "Sync Complete!\nContacts: ${contactResponse.message}\nLogs: ${logResponse.message}"
                    Toast.makeText(this@MainActivity, "Sync Successful", Toast.LENGTH_SHORT).show()
                }
                
                channel.shutdown()

            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    statusText.text = "Error: ${e.message}"
                    e.printStackTrace()
                }
            }
        }
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == PERMISSION_REQUEST_CODE) {
            if (grantResults.isNotEmpty() && grantResults.all { it == PackageManager.PERMISSION_GRANTED }) {
                startSync()
            } else {
                Toast.makeText(this, "Permissions required", Toast.LENGTH_SHORT).show()
            }
        }
    }
}
