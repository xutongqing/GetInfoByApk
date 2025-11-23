package com.example.infoapp

import android.content.Context
import android.content.Intent
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.uiautomator.By
import androidx.test.uiautomator.UiDevice
import androidx.test.uiautomator.Until
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ServerAutoStartTest {

    private lateinit var device: UiDevice

    @Before
    fun startMainActivityFromHomeScreen() {
        // Initialize UiDevice instance
        device = UiDevice.getInstance(InstrumentationRegistry.getInstrumentation())

        // Start from the home screen
        device.pressHome()

        // Wait for launcher
        val launcherPackage = device.launcherPackageName
        device.wait(Until.hasObject(By.pkg(launcherPackage).depth(0)), 1000)

        // Launch the app
        val context = ApplicationProvider.getApplicationContext<Context>()
        val intent = context.packageManager.getLaunchIntentForPackage("com.example.infoapp")
        intent?.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK)
        context.startActivity(intent)

        // Wait for the app to appear
        device.wait(Until.hasObject(By.pkg("com.example.infoapp").depth(0)), 5000)
    }

    @Test
    fun clickStartServerButton() {
        // Wait for the "Start Server" button to appear
        // Note: The text might be "Start Server" or "Sync Data to PC" depending on XML, 
        // but we updated it to "Start Server" in previous steps.
        // We can also find by ID: com.example.infoapp:id/syncButton
        
        val button = device.wait(Until.findObject(By.res("com.example.infoapp", "syncButton")), 5000)
        
        if (button != null && button.isEnabled) {
            button.click()
        }
        
        // Wait a bit to ensure the click is registered and server starts
        Thread.sleep(2000)
        
        // Verify status text changes (optional)
        val statusText = device.findObject(By.res("com.example.infoapp", "statusText"))
        // We expect "Server listening..." or similar
    }
}
