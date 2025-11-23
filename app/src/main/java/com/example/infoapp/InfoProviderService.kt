package com.example.infoapp

import android.content.Context
import com.example.infoapp.proto.DataTransferServiceGrpcKt
import com.example.infoapp.proto.InfoRequest
import com.example.infoapp.proto.InfoResponse
import com.example.infoapp.proto.InfoType
import com.example.infoapp.proto.ContactList
import com.example.infoapp.proto.CallLogList
import com.example.infoapp.utils.ContentReader

class InfoProviderService(private val androidContext: Context) : DataTransferServiceGrpcKt.DataTransferServiceCoroutineImplBase() {
    
    override suspend fun getInfo(request: InfoRequest): InfoResponse {
        val reader = ContentReader(androidContext)
        return when (request.type) {
            InfoType.CONTACTS -> {
                val contacts = reader.getContacts()
                val contactList = ContactList.newBuilder().addAllContacts(contacts).build()
                InfoResponse.newBuilder()
                    .setContacts(contactList)
                    .setMessage("Successfully retrieved ${contacts.size} contacts")
                    .build()
            }
            InfoType.CALL_LOGS -> {
                val logs = reader.getCallLogs()
                val logList = CallLogList.newBuilder().addAllLogs(logs).build()
                InfoResponse.newBuilder()
                    .setCallLogs(logList)
                    .setMessage("Successfully retrieved ${logs.size} call logs")
                    .build()
            }
            else -> {
                InfoResponse.newBuilder()
                    .setMessage("Unknown request type")
                    .build()
            }
        }
    }
}
