# socks-tun
SocksTun - Using a TUN device to access network resources via a Socks server.  Allows you to socksify outgoing connections by using a TUN device. Similar to SocksCap except it intercepts the TCP/IP data at network layer 3 instead of at network layer 4.

该项目fork自 https://github.com/normanr/socks-tun 可以把tap/tun上的tcp链接走socks代理服务器，对代码稍作修改可以支持最新版tap-windows https://github.com/OpenVPN/tap-windows6

原理是修改tun上的tcp/ip源目标地址及端口，使数据包重定向到指定的监听端口，这样就让所有走tun的tcp都连接到本地端口，再由程序把数据包转发到socks代理服务器上。

目前不支持udp，以后也许会添加对udp支持（缺少测试环境的情况下，实现这个可能性很低）。

TODO list
需要在路由表中添加socks服务器地址以避免从tun连socks服务器，如果是本地socks转发代理，则需要把socks的外网真实ip添加到路由表中，计划添加该部分功能，以方便使用。
为避免某些地址走tun，需要设置一个白名单ip路由表。
把windows设置成路由模式，可以让其他设备通过该工具走socks代理。
出一个整合包，把tap-windows一起整合进来，方便安装使用。







