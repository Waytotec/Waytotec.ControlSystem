/* usrcfg.h - Configuration header */

/* Copyright 2001 TAEBAEK Soft Corp. */

/*
modification history
--------------------
01a,01/08/2001,jmlee  written.
*/

/*
This file contains Configuration related constants and structures.
*/

#ifndef __IPINSTALL_H
#define __IPINSTALL_H

#define UCHAR	unsigned char
#define UINT8	unsigned char
#define UINT16	unsigned short
#define UINT32	unsigned int

#define FOURCC( ch0, ch1, ch2, ch3 ) (((UINT32)(UCHAR)(ch0) << 0) | ((UINT32)(UCHAR)(ch1) << 8) | ((UINT32)(UCHAR)(ch2) <<  16) | ((UINT32)(UCHAR)(ch3) <<  24))

/* Valid Configuration Signature */

#define CFG_SIG                         FOURCC('C','F','G','S')

#define MAX_STRING                      24
#define MAX_KEY	                      	64



#define DEFAULT_CONFIG_PORT             20011               /* Default Port Number */
#define EXTENSION	
#ifdef EXTENSION
//#define FW_VERSION		2
#define FW_VERSION		3
#else
#define FW_VERSION		1
#endif

/* Configuration Structure */

typedef struct {
    UINT8   ipMacAddress[6];            /* MAC Address */
    UINT32  ipAddress;                  /* IP Address */
    UINT32  ipMask;                     /* Subnet Mask */
	UINT32  ipGateway;
    char    targetName[MAX_STRING];     /* Target Name : Serial String*/ 
    char    targetUserName[MAX_STRING]; /* Target User Name */
    char    targetPassword[MAX_STRING]; /* Target Password */
    UINT32  sig;                        /* Signature */
	UINT32  useWlan;
	char	 ssid[MAX_STRING];
	UINT32	 authtype;
	UINT32	 keytype;	
	char	 key[MAX_KEY];
	UINT32	 httpport;
	UINT32   rtspport;
	UINT32   httpjpegport;
	UINT32   ptzport;

} CFG_NVRAM_STRUCT;

/* Remote Configuration Structure */

typedef struct {
    UINT32              msg;
#define RCFG_GETCONFIG                  FOURCC('G','C','F','G')
#define RCFG_GETCONFIGOK                FOURCC('G','C','O','K')
#define RCFG_SETCONFIG                  FOURCC('S','C','F','G')
#define RCFG_SETCONFIGOK                FOURCC('S','C','O','K')
#define RCFG_UPGRADE                    FOURCC('U','P','G','R')
#define RCFG_UPGRADEOK                  FOURCC('U','P','O','K')
#define RCFG_UPGRADEDOWN                FOURCC('U','P','D','N')
#define RCFG_UPGRADEWRITE               FOURCC('U','P','W','R')
#define RCFG_UPGRADEFAIL                FOURCC('U','P','F','A')
#define RCFG_REBOOT                     FOURCC('B','O','O','T')
#define RCFG_SETDEFAULT                 FOURCC('S','D','E','F')
    UINT32              version;
    CFG_NVRAM_STRUCT    cfgNvRam;
} REMOTE_CFG_STRUCT;

enum {
	AUTH_NONE = 0,
	AUTH_WEP64,
	AUTH_WEP128,
	AUTH_WPA_PSK_TKIP,
	AUTH_WPA2_PSK_TKIP
};
enum {
	LANTYPE_ELAN = 0,
	LANTYPE_WLAN
};

enum {
	KEY_ASCII = 0,
	KEY_HEX
};

/* Forward Declarations */



#endif  /* INCusrcfgh */
