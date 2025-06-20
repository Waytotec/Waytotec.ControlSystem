#ifndef _CAM_PROTO_H
#define _CAM_PROTO_H
#include "cam_client.h"

typedef struct _proto_t {
    char cmdsize[2];
    char total_size[12];
    unsigned char checksum;
    char cmdstr[64];
} proto_t;

typedef struct _pkt_t{
    int cmd;
    int  cmdhdrsize;
    int  totalsize;
    int error_flag;
    proto_t phdr;
    char data[2048];
} pkt_t;

typedef struct _upgrage_t{
    FILE *fp;
    char filename[128];
    int  filesize;
    int  seq;
    int update;
    int type;
    char platform[32];
    char day[32];
    char ext[32];
} upgrade_t;

#define PKTHDRSIZE  15 // len 2 + totalsize 12 + chsum 1 

#define ERR_NOERROR          0

#define ERR_RECV_TOTALSIZE      -1
#define ERR_RECV_CHECKSUM       -2
#define ERR_RECV_COM_STRING     -3
#define ERR_RECV_DATA           -4

#define ERR_CHECKSUM        -5
#define ERR_COM_STRING      -6

#define ERR_NOT_BUF         -7
#define ERR_NOT_SEARCH      -8
#define ERR_NOT_SERVER      -9
#define ERR_TEARDOWN        -10
#define ERR_EEPROM          -11
#define ERR_NO_SERCH_FILE   -12
#define ERR_GENERIC         -13

#define ERR_SAME_VERSION    -14
#define ERR_MESSAGE_FAIL    -15
#define ERR_MAX_CNT_FULL    -16
#define ERR_NOT_PROMISE     -17
#define ERR_SAVE_SEND_MAX_USER   -18

#define ERR_VALUE_INCONGRUITY   -19
#define ERR_SAVE_CONFIG          -20

#define NOT_PROMISE   "NOT_PROMISE"
#define NOTHING   "NOTHING"

#define RSP_SUCCESS "SUCCESS"
#define RSP_FAIL   "FAIL"

#define CAM_REBOOT          "CAM_REBOOT"
#define STR_FILEDOWNLOAD    "FILEDOWNLOAD"
#define STR_DOWNDATA        "DOWNDATA"
#define STR_UPGRADE         "UPGRADE"
#define STR_UPABORT         "UPABORT"
#define STR_CAMVERSION      "CAMVERSION"
#define CAM_SOFTDEFAULT         "SOFTDEFAULT"
#define CAM_HARDDEFAULT         "HARDDEFAULT"

#define UPDATE_IDLE        0
#define UPDATE_FILESET     1
#define UPDATE_DOWNDATA    2
#define UPDATE_UPGRADE     3

#define UPTYP_NONE      0
#define UPTYP_KILROG    1
#define UPTYP_SYSTEM    2
#define UPTYP_OPENWRT   3
#define UPTYP_OPENWRT_R 4 // openwrt restore


extern unsigned char get_checksum(char *buf, int size);
extern int mk_response(pkt_t *pkt, char *cmdstr, int error_flag);
extern int mk_response_msg(pkt_t *pkt, char *cmdstr, int error_flag, char *msg);
extern int cam_filedownload(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr);
extern int cam_downdata(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr);
extern int cam_upgrade(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr);
extern int cam_upabort(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr);
extern int cam_camversion(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *mdstr);
extern int cam_reboot(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr);
extern int cam_harddefault(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr);


#endif

