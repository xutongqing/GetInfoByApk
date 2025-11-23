package com.example.infoapp.utils

import android.content.Context
import android.provider.CallLog
import android.provider.ContactsContract
import com.example.infoapp.proto.CallLog as ProtoCallLog
import com.example.infoapp.proto.Contact as ProtoContact

class ContentReader(private val context: Context) {

    fun getContacts(): List<ProtoContact> {
        val contacts = mutableListOf<ProtoContact>()
        val cursor = context.contentResolver.query(
            ContactsContract.CommonDataKinds.Phone.CONTENT_URI,
            null, null, null, null
        )

        cursor?.use {
            val nameIndex = it.getColumnIndex(ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME)
            val numberIndex = it.getColumnIndex(ContactsContract.CommonDataKinds.Phone.NUMBER)

            while (it.moveToNext()) {
                if (nameIndex >= 0 && numberIndex >= 0) {
                    val name = it.getString(nameIndex) ?: "Unknown"
                    val number = it.getString(numberIndex) ?: ""
                    contacts.add(
                        ProtoContact.newBuilder()
                            .setName(name)
                            .setPhoneNumber(number)
                            .build()
                    )
                }
            }
        }
        return contacts
    }

    fun getCallLogs(): List<ProtoCallLog> {
        val logs = mutableListOf<ProtoCallLog>()
        val cursor = context.contentResolver.query(
            CallLog.Calls.CONTENT_URI,
            null, null, null, CallLog.Calls.DATE + " DESC"
        )

        cursor?.use {
            val numberIndex = it.getColumnIndex(CallLog.Calls.NUMBER)
            val typeIndex = it.getColumnIndex(CallLog.Calls.TYPE)
            val dateIndex = it.getColumnIndex(CallLog.Calls.DATE)
            val durationIndex = it.getColumnIndex(CallLog.Calls.DURATION)

            while (it.moveToNext()) {
                if (numberIndex >= 0) {
                    val number = it.getString(numberIndex) ?: "Unknown"
                    val type = when (it.getInt(typeIndex)) {
                        CallLog.Calls.INCOMING_TYPE -> "Incoming"
                        CallLog.Calls.OUTGOING_TYPE -> "Outgoing"
                        CallLog.Calls.MISSED_TYPE -> "Missed"
                        else -> "Unknown"
                    }
                    val date = it.getLong(dateIndex)
                    val duration = it.getLong(durationIndex)

                    logs.add(
                        ProtoCallLog.newBuilder()
                            .setNumber(number)
                            .setType(type)
                            .setDate(date)
                            .setDuration(duration)
                            .build()
                    )
                }
            }
        }
        return logs
    }
}
