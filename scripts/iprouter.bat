reg add HKLM\system\currentcontrolset\services\tcpip\parameters /v ipenablerouter /t REG_DWORD /d 1 /f
sc config remoteaccess start=auto
net start remoteaccess
msg * "Remember to reboot your computer."
