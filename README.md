# socks-tun

该项目fork自 <https://github.com/normanr/socks-tun> 可以把tap/tun上的tcp/udp数据包重定向到socks代理，对代码稍作修改可以支持最新版tap-windows <https://github.com/OpenVPN/tap-windows6>

原项目英文说明：

>SocksTun - Using a TUN device to access network resources via a Socks server.  Allows you to socksify outgoing connections by using a TUN device. Similar to SocksCap except it intercepts the TCP/IP data at network layer 3 instead of at network layer 4.


原理是修改tun上的tcp/ip源目标地址及端口，使数据包重定向到指定的监听端口，这样就让所有走tun的tcp都连接到本地端口，再由程序把数据包转发到socks代理服务器上。

## 改动

1. 添加对udp支持，这个功能目前还是实验阶段，需要在编译时添加条件编译符号USEUDP，否则所有发送到tap上UDP包将被抛弃。

2. 关闭了tap上DHCP自动网关功能，添加路由管理，并添加自动识别系统默认网关、DNS功能，已正确添加相应路由，由于此功能需要管理员权限，所以该工具启动时会请求管理员权限。

3. 移除Windows Service支持，把此工具仅视作一个代理转发小工具，按需启动。

4. 添加了若干配置，以方便使用。

## 下载

整合包下载页面 https://github.com/john-guo/socks-tun/releases

## 使用

首先安装压缩包内tap-windows.exe，安装完成后保证127.0.0.1:1080（一般socks5服务器工具的默认地址，如ss之类）有socks5服务的情况下启动SocksTun.exe即可。

iprouter.bat以及iprouter_disable.bat是开启关闭本机IP转发功能，开启后只要有其他设备与本机在同一网段内把默认网关设置为本机真实网卡IP地址即可让该设备也使用socks5服务（无线设备可能需要在无线路由器关闭AP隔离）。

### 配置文件说明

    IPAddress Tap的本地IP地址（Tap网关地址）

    RemoteNetwork Tap目标网络地址，默认为0.0.0.0表示所有网络

    RemoteNetmask Tap目标子网掩码，默认0.0.0.0表示所有网络

    DHCPServer Tap上的DHCP服务器地址，使用DHCP方式设定Tap本地ip地址，以后可能会使用静态IP分配方式取代这些DHCP配置。

    DHCPNetmask Tap的本地子网掩码，此掩码必须能让DHCPServer和IPAddress在同一个网段内

    DHCPLeaseTime DHCP租期

    SocksPort TCP/UDP转发用中间代理端口

    LogPort 日志端口，远程读取日志用

    Services 服务配置，保留，暂未用到

    TunTapDevice Tap网卡名，默认为空表示自动查找，如果安装有多快Tap需要用此选择正确Tap

    ProxyAddress socks5服务器地址

    ProxyPort socks5服务器端口

    UDPTimeoutSeconds UDP连接超时设定

## TODO list

1. 增加本地DNS转发功能，避免DNS污染。

2. 增加路由自定义规则，黑名单白名单制度等。

3. 更完善的启动网络环境检测、配置。

4. 更完善的UDP转发机制。

5. 更方便的本机IP转发功能，无需批处理，并能提示可配置网关地址。

6. 增加UI界面，更丰富的各种提示信息。
