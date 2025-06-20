#include <stdio.h>
#include <stdlib.h>        /* for malloc, free */
#include <stdint.h>        /* for uint32_t */
#include <stddef.h>        /* for offsetof */
#include <string.h>        /* for strcmp */
#include <syslog.h>        /* for log levels */
#include <unistd.h>        /* for gethostname */
#include <errno.h>         /* for errno, EINVAL */
#include <asm/types.h>     /* for linux/netlink.h */
#include <sys/ioctl.h>     /* for SIOCGIFCONF */
#include <arpa/inet.h>     /* for inet_ntoa */
#include <net/if.h>        /* for IFNAMSIZ, ifreq */
#include <netinet/in.h>    /* for sockaddr */
#include <linux/netlink.h> /* for sockaddr_nl */

#include <uci.h>

#include "ipinstall.h"
#include "network_util.h"
#include "uci_conf.h"

#define _(x) x

#define RTACTION_ADD   1
#define RTACTION_DEL   2
#define RTACTION_HELP  3
#define RTACTION_FLUSH 4
#define RTACTION_SHOW  5

#define E_NOTFOUND      8
#define E_SOCK          7
#define E_LOOKUP        6
#define E_VERSION       5
#define E_USAGE         4
#define E_OPTERR        3
#define E_INTERN        2
#define E_NOSUPP        1


#define DEBUG_TRACE 1
#define INVALID_SOCKET      -1
#define ERROR   -1

#define FLAG_ON     1
#define FLAG_OFF    0

#define FLAG_ELAN   0
#define FLAG_WLAN 1
#define FLAG_BROA   2

//#define WLAN
//#define IFWLAN

//#define WIPIFNAME    "tiwlan0"
#define WIPIFNAME    "eth0:1"
#define EIPIFNAME    "eth0"

//#define STREAMPORT   8880
//#define C485PORT     8883
//#define DAEMONPORT  8884
//#define GLOBALPORT  8885
//#define WLANPORT  8893


typedef struct rt_info {
    int eth0_flag;
    int wlan0_flag;
    int default_flag;
    int gateway_if;
    char elan[16];
    char elanmac[6];
    char wlan[16];
    char wlanmac[6];
    char gateway[16];
    unsigned int elanip;
    unsigned int elanmask;
    unsigned int wlanip;
    unsigned int wlanmask;
    unsigned int gw;
} rt_info_t;

int sConfig = INVALID_SOCKET;
CFG_NVRAM_STRUCT cfgNvRam, cfgNvRam_w;

static rt_info_t rtinfo;
int selecteth = FLAG_ELAN;
char GateWay[16];

void show_usage(char *s)
{
	printf("show usage %s\n",s);
	return;
}

void cfgSockClose(void)
{
	close(sConfig);
	sConfig = INVALID_SOCKET;
}

void cfgSockCreate(void)
{
	int optval = 1;
	struct sockaddr_in sin;

	/* Create socket. */
	sConfig = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
	if (sConfig == INVALID_SOCKET)
	{
		printf("ERROR\n    Failed to create configuration socket\n");
		return;
	}

	/* Enable Broadcast. */
	if (setsockopt(sConfig, SOL_SOCKET, SO_BROADCAST, (char *)&optval, sizeof(optval)) == ERROR)
	{
		printf("Failed to set configuration socket option\n");
		cfgSockClose();
		return;
	}

	sin.sin_family = AF_INET;
	sin.sin_addr.s_addr = INADDR_ANY;
	sin.sin_port = htons(DEFAULT_CONFIG_PORT);

	/* Bind socket. */
	if (bind(sConfig, (struct sockaddr *)&sin, sizeof(sin)) < 0)
	{
		printf("ERROR\n    Failed to bind configuration socket\n");
		cfgSockClose();
		return;
	}
}


int cfgGetInfo()
{
    memset(&rtinfo,0,sizeof(rt_info_t));
    memset(&cfgNvRam, 0, sizeof(cfgNvRam));
    memset(&cfgNvRam_w, 0, sizeof(cfgNvRam_w));
}

int GetGw(int type,unsigned char *gw)
{
    int lan;
    unsigned char *p;
    int i;
    if( type == LANTYPE_ELAN ){
        lan = (rtinfo.elanip & rtinfo.elanmask) | 0x01000000;
    } else {
        lan = (rtinfo.wlanip & rtinfo.wlanmask) | 0x01000000;
    }
    p = ( unsigned char *)&lan;
    sprintf(gw,"%d.%d.%d.%d",p[0],p[1],p[2],p[3]);
    return 0;
}

int GetGwIf()
{
	int elan,wlan;
#ifndef IFWLAN
	return LANTYPE_ELAN;
#endif

	elan = rtinfo.elanip & rtinfo.elanmask;
	wlan = rtinfo.wlanip & rtinfo.wlanmask;

	if( elan == (rtinfo.gw & rtinfo.elanmask) )
		return LANTYPE_ELAN;
	else if( wlan == (rtinfo.gw & rtinfo.wlanmask) )
		return LANTYPE_WLAN;
	else
		return -1;
}

#ifdef WLAN
int cfgGetData()
{
	int i;		/* generic loop counter */
	char buf[128];
	char Serial[18];
	char WebPort[18];
	unsigned char *p;
	char *config_return;
	CFG_NVRAM_STRUCT *cfg = &cfgNvRam, *cfg_w = &cfgNvRam_w;
	cfg->sig = CFG_SIG;
	cfg_w->sig = CFG_SIG;
	Interface Interface_data;
	HostRow Host_data;

	readIpAddress(EIPIFNAME, &Interface_data);
	readNetmask(EIPIFNAME, &Interface_data);
	readMacAddress(EIPIFNAME,&Interface_data);
	printf("EMAC : %s\n", Interface_data.MacAddress);
	cfg->ipAddress = inet_addr(Interface_data.IpAddress);
	cfg->ipMask = inet_addr(Interface_data.Mask);
	sscanf(Interface_data.MacAddress, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx", &cfg->ipMacAddress[0],
		&cfg->ipMacAddress[1], &cfg->ipMacAddress[2], &cfg->ipMacAddress[3], &cfg->ipMacAddress[4], &cfg->ipMacAddress[5]);

	strcpy(rtinfo.elan, Interface_data.IpAddress);
	rtinfo.elanip = cfg->ipAddress;
	rtinfo.elanmask = cfg->ipMask;
	memcpy(rtinfo.elanmac, cfg->ipMacAddress, sizeof(rtinfo.elanmac));

	memset(&Interface_data, 0, sizeof(Interface_data));
	readIpAddress(WIPIFNAME, &Interface_data);
	readNetmask(WIPIFNAME, &Interface_data);
	readMacAddress(WIPIFNAME,&Interface_data);
	printf("WMAC : %s\n", Interface_data.MacAddress);
	cfg_w->ipAddress = inet_addr(Interface_data.IpAddress);
	cfg_w->ipMask = inet_addr(Interface_data.Mask);
	sscanf(Interface_data.MacAddress, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx", &cfg_w->ipMacAddress[0],
		&cfg_w->ipMacAddress[1], &cfg_w->ipMacAddress[2], &cfg_w->ipMacAddress[3], &cfg_w->ipMacAddress[4], &cfg_w->ipMacAddress[5]);

	strcpy(rtinfo.wlan, Interface_data.IpAddress);
	rtinfo.wlanip = cfg_w->ipAddress;
	rtinfo.wlanmask = cfg_w->ipMask;
	memcpy(rtinfo.wlanmac, cfg_w->ipMacAddress, sizeof(rtinfo.wlanmac));

	readSerial(&Host_data);
	readGateway(&Host_data);
	if(!strcmp(Host_data.gateway, "")) {
		rtinfo.gw = (rtinfo.elanip & rtinfo.elanmask) | 0x01000000;
		p = (unsigned char *)&rtinfo.gw;
		sprintf(Host_data.gateway, "%d.%d.%d.%d",p[0],p[1],p[2],p[2]);
		sprintf(buf,"route add default gw %s",Host_data.gateway);
		system(buf);
	}

	cfg->ipGateway = inet_addr(Host_data.gateway);
	cfg_w->ipGateway = inet_addr(Host_data.gateway);
	strcpy(rtinfo.gateway,Host_data.gateway);
	strcpy(GateWay,Host_data.gateway);
	strcpy(Serial, Host_data.hostname);

	rtinfo.gw = cfg->ipGateway;

	printf("eip : %s\nwip : %s\nhostname : %s\ngateway : %s\n", rtinfo.elan, rtinfo.wlan, Serial, rtinfo.gateway);

	strcpy(cfg->targetUserName, "admin");
	strcpy(cfg->targetPassword, "admin");
	strcpy(cfg_w->targetUserName, "admin");
	strcpy(cfg_w->targetPassword, "admin");

	conf_init(NULL);

	config_return = conf_get("uhttpd.main.listen_http");
	if(config_return) {
		if(strchr(conf_get("uhttpd.main.listen_http"), ':'))
			strcpy(WebPort, strchr(conf_get("uhttpd.main.listen_http"), ':')+1);
		else
		{
			strcpy(WebPort, conf_get("uhttpd.main.listen_http"));
		}
	}
	else
		strcpy(WebPort, "4011");

	sprintf(cfg->targetName,"%s:%s",Serial,WebPort);
	sprintf(cfg_w->targetName,"%s:%s",Serial,WebPort);
	cfg->useWlan = LANTYPE_ELAN;
	cfg->httpport = atoi(WebPort);
	cfg_w->useWlan = LANTYPE_WLAN;
	cfg_w->httpport = atoi(WebPort);

	config_return = conf_get("rtsp.@server[0].port");
	cfg->rtspport = config_return ? atoi(config_return) : 554;
	cfg_w->rtspport = cfg->rtspport;
	config_return = conf_get("nexpa.jpeg.http_port");
	cfg->httpjpegport = config_return ? atoi(config_return) : 8080;
	cfg_w->httpjpegport = cfg->httpjpegport;

	conf_exit();

	printf("get if %d\n",GetGwIf());
}

#else
int cfgGetData()
{
	int i;		/* generic loop counter */
	char buf[128];
	char Serial[18];
	char WebPort[18];
	unsigned char *p;
	char *config_return;
	CFG_NVRAM_STRUCT *cfg = &cfgNvRam;
	cfg->sig = CFG_SIG;
	Interface Interface_data;
	HostRow Host_data;

	readIpAddress(EIPIFNAME, &Interface_data);
	readNetmask(EIPIFNAME, &Interface_data);
	readMacAddress(EIPIFNAME,&Interface_data);
	readSerial(&Host_data);
	readGateway(&Host_data);
	if(!strcmp(Host_data.gateway, "")) {
		rtinfo.gw = (rtinfo.elanip & rtinfo.elanmask) | 0x01000000;
		p = (unsigned char *)&rtinfo.gw;
		sprintf(Host_data.gateway, "%d.%d.%d.%d",p[0],p[1],p[2],p[2]);
		sprintf(buf,"route add default gw %s",Host_data.gateway);
		system(buf);
	}

	cfg->ipAddress = inet_addr(Interface_data.IpAddress);
	cfg->ipMask = inet_addr(Interface_data.Mask);
	sscanf(Interface_data.MacAddress, "%hhx:%hhx:%hhx:%hhx:%hhx:%hhx", &cfg->ipMacAddress[0],
		&cfg->ipMacAddress[1], &cfg->ipMacAddress[2], &cfg->ipMacAddress[3], &cfg->ipMacAddress[4], &cfg->ipMacAddress[5]);

	strcpy(rtinfo.elan, Interface_data.IpAddress);
	rtinfo.elanip = cfg->ipAddress;
	rtinfo.elanmask = cfg->ipMask;
	memcpy(rtinfo.elanmac, cfg->ipMacAddress, sizeof(rtinfo.elanmac));

	cfg->ipGateway = inet_addr(Host_data.gateway);
	strcpy(GateWay,Host_data.gateway);
	strcpy(Serial, Host_data.hostname);

	rtinfo.gw = cfg->ipGateway;

	printf("ip : %s\nmask : %s\nhostname : %s\ngateway : %s\n", Interface_data.IpAddress, Interface_data.Mask, Serial, GateWay);

	strcpy(cfg->targetUserName, "admin");
	strcpy(cfg->targetPassword, "admin");

	conf_init(NULL);

	config_return = conf_get("uhttpd.main.listen_http");
	if(config_return) {
		if(strchr(conf_get("uhttpd.main.listen_http"), ':'))
			strcpy(WebPort, strchr(conf_get("uhttpd.main.listen_http"), ':')+1);
		else
		{
			strcpy(WebPort, conf_get("uhttpd.main.listen_http"));
		}
	}
	else
		strcpy(WebPort, "4011");

	sprintf(cfg->targetName,"%s:%s",Serial,WebPort);
	cfg->useWlan = LANTYPE_ELAN;
	cfg->httpport = atoi(WebPort);

	config_return = conf_get("media.@server[0].port");
	cfg->rtspport = config_return ? atoi(config_return) : 554;
	config_return = conf_get("jpeg_send.@led[0].tcp_port");
	cfg->httpjpegport = config_return ? atoi(config_return) : 8080;

	// config_return = PQgetvalue(res, 0, 0);
	// cfg->ptzport = config_return ? atoi(config_return) : 0;

	conf_exit();

	printf("get if %d\n",GetGwIf());
}
#endif

static struct uci_context *ctx;

void cli_perror(){
}

static int uci_rpc_commit(char *tuple)
{
	struct uci_element *e = NULL;
	struct uci_ptr ptr;
	int ret = 0;

	if (uci_lookup_ptr(ctx, &ptr, tuple, true) != UCI_OK) {
		cli_perror();
		return 1;
	}

	if (uci_commit(ctx, &ptr.p, false) != UCI_OK) {
		cli_perror();
		ret = 1;
	}

	if (ptr.p)
		uci_unload(ctx, ptr.p);

	return ret;
}

static int uci_rpc_set(char *tuple){
	struct uci_ptr ptr;
	int ret = UCI_OK;

	if (uci_lookup_ptr(ctx, &ptr, tuple, true) != UCI_OK) {
		cli_perror();
		return 1;
	}

	ret = uci_set(ctx, &ptr);

	/* save changes, but don't commit them yet */
	if (ret == UCI_OK)
		ret = uci_save(ctx, ptr.p);

	if (ret != UCI_OK) {
		cli_perror();
		return 3;
	}

	if (ptr.p)
		uci_unload(ctx, ptr.p);

	return 0;
}

int cfgSetDataAll( CFG_NVRAM_STRUCT *cfg)
{
	int i;                  /* generic loop counter */
	unsigned char *s,*d;
	UINT32 g;
	struct in_addr addr;
	char ip[32], mask[32], gateway[32];
	int ret = 0;
	char path[64];

	s = (unsigned char *)&cfg->ipAddress;
	d = (unsigned char *)&addr;
	for( i = 0; i < 4; i++) d[i]=s[i];
		sprintf(ip,"%s",inet_ntoa(addr));
		
	s =  (unsigned char *)&cfg->ipMask;
	for( i = 0; i < 4; i++) d[i]=s[i];
		sprintf(mask,"%s",inet_ntoa(addr));
		
	s =  (unsigned char *)&cfg->ipGateway;
	for( i = 0; i < 4; i++) d[i]=s[i];
		sprintf(gateway,"%s",inet_ntoa(addr));

	ctx = uci_alloc_context();
	if (!ctx) {
		printf("Out of memory\n");
		return 1;
	}

	sprintf(path, "network.lan.proto=static");
	ret = uci_rpc_set(path);
	printf("%s %d\n", path, ret);

	sprintf(path, "network.lan.ipaddr=%s", ip);
	ret = uci_rpc_set(path);
	printf("%s %d\n", path, ret);

	sprintf(path, "network.lan.netmask=%s", mask);
	ret = uci_rpc_set(path);
	printf("%s %d\n", path, ret);

	sprintf(path, "network.lan.gateway=%s", gateway);
	ret = uci_rpc_set(path);
	printf("%s %d\n", path, ret);

	sprintf(path, "network.lan.dns=%s", "168.126.63.1");
	ret = uci_rpc_set(path);
	printf("%s %d\n", path, ret);

	uci_rpc_commit(path);

	uci_free_context(ctx);
	return 0;
}

void cfgSetDefault(CFG_NVRAM_STRUCT *cfg)
{
	printf("cfgSetDefault call\n");
}

void cfgDaemonTask(void)
{
	char strIP[32];
	struct sockaddr_in saFrom;
	int saFromLen;
	REMOTE_CFG_STRUCT cfgRemote;
	struct sockaddr_in saTo;
	int err;
	char *p;
	int i;
	char buf[64];
	char strGW[32];

	while(1)
	{
		if( sConfig != INVALID_SOCKET )
			cfgSockClose();
		cfgSockCreate();
		memset((char *)&saFrom, 0, sizeof(saFrom));
		saFromLen = sizeof(saFrom);

		/* Receive configuration request. */
		if (recvfrom(
			sConfig,
			(char *)&cfgRemote,
			sizeof(cfgRemote),
			0,
			(struct sockaddr *)&saFrom,
			&saFromLen
			) != sizeof(cfgRemote))
		{
			printf("CFGSOCK: ERROR on recvfrom()\n");
			continue;
		}
		/* Verify version. */
		if ((cfgRemote.cfgNvRam.sig != CFG_SIG) || (cfgNvRam.sig != CFG_SIG))
		{
#ifdef DEBUG_TRACE
			printf("Invalid message\n");
			printf("cfgRemotesig : %08x\n",cfgRemote.cfgNvRam.sig);
			printf("cfgRemotesig : %08x\n",cfgNvRam.sig);           
			printf("CFG_SIG      : %08x\n",CFG_SIG);            
			p = &cfgRemote.cfgNvRam.sig;
			printf("%c %c %c %c\n",p[0],p[1],p[2],p[3]);
#endif
			continue;
		}

		 p = inet_ntoa(saFrom.sin_addr);
		 strcpy(strIP,p);
#ifdef DEBUG_TRACE
		printf("1 Configuration message from %s\n", strIP);
#endif  /* DEBUG_TRACE */

		/* If not broadcast or addressed to us, discard message. */
		if ((cfgRemote.cfgNvRam.ipMacAddress[0] == cfgNvRam.ipMacAddress[0]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[1] == cfgNvRam.ipMacAddress[1]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[2] == cfgNvRam.ipMacAddress[2]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[3] == cfgNvRam.ipMacAddress[3]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[4] == cfgNvRam.ipMacAddress[4]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[5] == cfgNvRam.ipMacAddress[5]))
		{
			if(DEBUG_TRACE ) printf("eth0 UNICAST, ");
			selecteth = FLAG_ELAN;
		}
#ifdef IFWLAN
		else if ((cfgRemote.cfgNvRam.ipMacAddress[0] == cfgNvRam_w.ipMacAddress[0]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[1] == cfgNvRam_w.ipMacAddress[1]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[2] == cfgNvRam_w.ipMacAddress[2]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[3] == cfgNvRam_w.ipMacAddress[3]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[4] == cfgNvRam_w.ipMacAddress[4]) &&
			(cfgRemote.cfgNvRam.ipMacAddress[5] == cfgNvRam_w.ipMacAddress[5]))
		{
			if(DEBUG_TRACE ) printf("eth1 UNICAST, ");
			selecteth = FLAG_WLAN;
		}
#endif
		else if ((cfgRemote.cfgNvRam.ipMacAddress[0] == 0xff) &&
			(cfgRemote.cfgNvRam.ipMacAddress[1] == 0xff) &&
			(cfgRemote.cfgNvRam.ipMacAddress[2] == 0xff) &&
			(cfgRemote.cfgNvRam.ipMacAddress[3] == 0xff) &&
			(cfgRemote.cfgNvRam.ipMacAddress[4] == 0xff) &&
			(cfgRemote.cfgNvRam.ipMacAddress[5] == 0xff))
		{
			if(DEBUG_TRACE ) printf("BROADCAST, ");
			selecteth = FLAG_BROA;
		}else{
			if(DEBUG_TRACE ) printf("2 Configuration message from %s\n", strIP);
				continue;
		}

		switch (cfgRemote.msg)
		{
		case RCFG_GETCONFIG:
#ifdef  DEBUG_TRACE
			printf("RCFG_GETCONFIG\n");
#endif  /* DEBUG_TRACE */

			cfgRemote.msg = RCFG_GETCONFIGOK;
			cfgRemote.version = FW_VERSION;

			cfgGetData();
			if( GetGwIf() == LANTYPE_ELAN )
				memcpy((void *)&cfgRemote.cfgNvRam, (void *)&cfgNvRam, sizeof(cfgNvRam));
			else
				memcpy((void *)&cfgRemote.cfgNvRam, (void *)&cfgNvRam_w, sizeof(cfgNvRam_w));               

			memset((char *)&saTo, 0, sizeof(saTo));

			/* Build acknowledgement. */
			saTo.sin_family = AF_INET;
			saTo.sin_addr.s_addr = INADDR_BROADCAST;
			saTo.sin_port = htons(DEFAULT_CONFIG_PORT);

			/* Send acknowledgement. */
			printf("sConfig is %d ,, saTo is %x \n", sConfig, &saTo);
			err = sendto(sConfig, (char *)&cfgRemote, sizeof(cfgRemote), 0, (struct sockaddr *)&saTo, sizeof(saTo));
			if (err != sizeof(cfgRemote))
			{
				printf("1 ERROR on sendto(), %d\n", err);
				perror("send error");
			}
#ifdef IFWLAN
			//system("route del default");
			system("route del 255.255.255.255 1>/dev/null 2>/dev/null");

			if( GetGwIf() == LANTYPE_ELAN ){
				GetGw(LANTYPE_WLAN,strGW);
				//sprintf(buf,"route add default gw %s",strGW);
				sprintf(buf,"route add 255.255.255.255 gw %s",strGW);
				memcpy((void *)&cfgRemote.cfgNvRam, (void *)&cfgNvRam_w, sizeof(cfgNvRam_w));
			} else {
				GetGw(LANTYPE_ELAN,strGW);
				//sprintf(buf,"route add default gw %s",strGW);
				sprintf(buf,"route add 255.255.255.255 gw %s",strGW);
				memcpy((void *)&cfgRemote.cfgNvRam, (void *)&cfgNvRam, sizeof(cfgNvRam));               
			}
			system(buf);

			memset(&saTo, 0, sizeof(saTo));

			/* Build acknowledgement. */
			saTo.sin_family         = AF_INET;
			saTo.sin_addr.s_addr    = INADDR_BROADCAST;
			saTo.sin_port           = htons(DEFAULT_CONFIG_PORT);
			sleep(2);
			err = sendto(sConfig, (char *)&cfgRemote, sizeof(cfgRemote), 0, (struct sockaddr *)&saTo, sizeof(saTo));
			if (err != sizeof(cfgRemote))
			{
				printf("2 ERROR on sendto(), %d\n", err);
				perror("send error");
			}
			//system("route del default");
			system("route del 255.255.255.255");
			//sprintf(buf,"route add default gw %s",GateWay);
			//system(buf);
#endif
			break;

		case RCFG_SETCONFIG:
#ifdef  DEBUG_TRACE
			printf("RCFG_SETCONFIG\n");
#endif  /* DEBUG_TRACE */

			if( (cfgRemote.cfgNvRam.ipAddress & cfgRemote.cfgNvRam.ipMask) != (cfgRemote.cfgNvRam.ipGateway & cfgRemote.cfgNvRam.ipMask) ){
				printf("ip gw mismatch\n");
				break;
			}

			cfgRemote.msg = RCFG_SETCONFIGOK;

			memcpy((void *)&cfgNvRam, (void *)&cfgRemote.cfgNvRam, sizeof(cfgNvRam));

			memset(&saTo, 0, sizeof(saTo));

			/* Build acknowledgement. */
			saTo.sin_family = AF_INET;
			saTo.sin_addr.s_addr = INADDR_BROADCAST;
			saTo.sin_port = htons(DEFAULT_CONFIG_PORT);
#ifdef IFWLAN
			if( selecteth == FLAG_ELAN && (GetGwIf() == LANTYPE_WLAN)){
				//system("route del default");
				system("route del 255.255.255.255 1>/dev/null 2>/dev/null");
				GetGw(LANTYPE_ELAN,strGW);
				//sprintf(buf,"route add default gw %s",strGW);
				sprintf(buf,"route add 255.255.255.255 gw %s",strGW);
				system(buf);
			} else if( selecteth == FLAG_WLAN && (GetGwIf() == LANTYPE_ELAN)){
				//system("route del default");
				system("route del 255.255.255.255 1>/dev/null 2>/dev/null");
				GetGw(LANTYPE_WLAN,strGW);
				//sprintf(buf,"route add default gw %s",strGW);
				sprintf(buf,"route add 255.255.255.255 gw %s",strGW);
				system(buf);
			}
#endif          
			/* Send acknowledgement. */
			err = sendto(sConfig, (char *)&cfgRemote, sizeof(cfgRemote), 0, (struct sockaddr *)&saTo, sizeof(saTo));
			if (err != sizeof(cfgRemote))
			{
				printf("3 ERROR on sendto(), %d\n", err);
			}

			
#ifdef IFWLAN           
			if( selecteth == FLAG_ELAN && (GetGwIf() == LANTYPE_WLAN)){
				//system("route del default");
				system("route del 255.255.255.255");
				//sprintf(buf,"route add default gw %s",GateWay);
				//system(buf);
			} else if( selecteth == FLAG_WLAN && (GetGwIf() == LANTYPE_ELAN)){
				//system("route del default");
				system("route del 255.255.255.255");
				//sprintf(buf,"route add default gw %s",GateWay);
				//system(buf);
			}
#endif

			cfgSetDataAll(&cfgNvRam);
			system("/etc/init.d/network restart");
			break;
			
		case RCFG_SETDEFAULT:
			printf("RCFG_SETDEFAULT\n");            
			memset((char *)&cfgNvRam, 0xff, sizeof(cfgNvRam));
			cfgSetDefault(&cfgNvRam);

			/* Preserve the MAC address. */
			//            bcopy((char *)sysFecEnetAddr, (char *)cfgNvRam.ipMacAddress, 6);

			/* Save configuration. */
			//            cfgSetNvRam();
			system("/sbin/reboot");
			//            reboot(BOOT_CLEAR);
			break;

		case RCFG_UPGRADE:
#ifdef  DEBUG_TRACE
			printf("RCFG_UPGRADE\n");
#endif  /* DEBUG_TRACE */
			cfgGetInfo();
			cfgRemote.msg = RCFG_UPGRADEOK;

			memset(&saTo, 0, sizeof(saTo));

			/* Build acknowledgement. */
			saTo.sin_family         = AF_INET;
			saTo.sin_addr.s_addr    = INADDR_BROADCAST;
			saTo.sin_port           = htons(DEFAULT_CONFIG_PORT);

			/* Send acknowledgement. */
			err = sendto(sConfig, (char *)&cfgRemote, sizeof(cfgRemote), 0, (struct sockaddr *)&saTo, sizeof(saTo));
			if (err != sizeof(cfgRemote))
			{
				printf("4 ERROR on sendto(), %d\n", err);
			}
			break;

		case RCFG_REBOOT:
#ifdef  DEBUG_TRACE
			printf("RCFG_REBOOT\n");
#endif  /* DEBUG_TRACE */
			system("/sbin/reboot");
			//            reboot(BOOT_CLEAR);
			break;
		default:
			break;
		}
	}
}

int main(int argc, char *argv[])
{
	cfgGetInfo();
	cfgGetData();
	cfgDaemonTask();
}
