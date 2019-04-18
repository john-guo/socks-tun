net stop remoteaccess
sc config remoteaccess start=disabled
reg add HKLM\system\currentcontrolset\services\tcpip\parameters /v ipenablerouter /t REG_DWORD /d 0 /f
msg * "Remember to reboot your computer."
