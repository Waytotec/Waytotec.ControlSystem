#include <unistd.h>
#include <stdio.h>
#include <stdlib.h>
#include <signal.h>
#include <time.h>
#include <fcntl.h>
#include <unistd.h>

#include <sys/socket.h>
#include <string.h>
#include <sys/vfs.h>
#include <syslog.h>
#include <sys/types.h>
#include <regex.h>
#include <ctype.h>

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>

#include "server.h"
#include "cam_server.h"
#include "cam_client.h"
#include "cam_proto.h"

#include "logprt.h"
#include "strutil.h"

#define KILROGDIR       "/tmp"
#define SYSTEMDIR       "/var"
#define OPENWRTDIR      "/tmp"

#define SIZE_DNHDRSIZE  10
#define SIZE_SEQSIZE    13


#define PO_FILENAME  0
#define PO_FILESIZE       1
#define PO_DOWNLOADMAX     2

#define PO_DEFAULT      2
#define PO_UPDATMAX     3

#define IT_INT          0
#define IT_STRING       1
#define IT_ENFLAG       2
#define IT_ONFLAG       3
#define IT_VIDEOSIZE    4
#define IT_BAUDRATE      5
#define IT_CODEC        6
#define IT_RCTL         7
#define IT_QUALITY      8
#define IT_FPS          9
#define IT_NETWORKMODE  10
#define IT_LOWHIGH      11
#define IT_NONC         12
#define IT_TIMEMODE     13
#define IT_TIMEZONE     14
#define IT_PANTILT      15
#define IT_ZOOM         16
#define IT_LENS         17
#define IT_EXPOSURE    18
#define IT_MSHUTTER    19
#define IT_FREQUENCY    20
#define IT_PTZPROTO    21
#define IT_RECMODE        22
#define IT_DDNSPROVIDER     23
#define IT_RESOLUTION        24
#define IT_IDOD             25
#define IT_DAYNIGHT        26
#define IT_VIDEOTYPE        27
#define IT_LANGUAGE        28
#define IT_SRESOLUTION       29
#define IT_PORT             30
#define IT_EMAIL           31
#define IT_IPADDRESS       32
#define IT_NETMASK            33
#define IT_HOSTNAME        34
#define IT_SERVERADDR      35


typedef struct _pvalue_t{
    int flag;
    char value[128];
    char sqlvalue[128];
    char *name;
} pvalue_t;

typedef struct _pname_t {
    int type;
    char *item;
    char *name;
} pname_t;
typedef struct _value_minmax_t
{
    char *name;
    int min;
    int max;
} value_minmax_t;

pname_t CAMFileDownSet[] = {
        {IT_STRING, "FILENAME", ""},
        {IT_INT, "SIZE", ""},            
        {0,       "",     ""}
};

pname_t CAMUPDateSet[] = {
        {IT_STRING, "FILENAME", ""},
        {IT_INT, "SIZE", ""},
        {IT_INT, "DEFAULT", ""},
        {0,       "",     ""}
};

char *EnFlag[] = {
    "DISABLE",
    "ENABLE",
    ""
};
char *OnFlag[] = {
    "OFF",
    "ON",
    ""
};
char *DdnsProvider[] = {
    "DYDNS",
    ""
};

char *VideoSize[] = {
    "720P_D1",
    "D1_D1",
    "D1_CIF",
    "1080P",
    "720P",
    "D1",
    "CIF",
    ""
};


char *ResolutionFishEye[] = {
    "5M",
    "960",
    "XGA",
    "624",
    "VGA",
    ""
};

char *Resolution1080P[] = {
    "1080P",
    "720P",
    "D1",
    "CIF",    
    ""
};

char *Resolution720P[] = {
    "720P",
    "D1",
    "CIF",    
    ""
};

char *ResolutionD1[] = {
    "D1",
    "HALFD1",
    "D1",
    "CIF",    
    "QVGA",
    ""
};

char *ResolutionSlave[] = {
    "D1",
    "CIF",
    ""
};

char *BaudRate[] = {
    "2400",
    "4800",
    "9600",
    "19200",
    "38400",
    "57600",
    "115200",
    ""
};
char *Codec[] = {
    "H264",
    "MPEG4",
    "MJPEG",
    ""
};
char *Rctl[] = {
    "VBR",
    "CBR",
    "CVBR",
    ""
};
char *Quality[] = {
    "HIGHEST",
    "HIGH",
    "MEDIUM",
    "LOW",
    "LOWEST",
    ""
};
char *Fps[] = {
    "30",
    "20",
    "15",
    "10",
    "5",
    ""
};
char *NetworkMode[] = {
    "STATIC",
    "DHCP",
    "PPPOE"
    ""
};
char *LowHigh[] = {
    "LOW",
    "HIGH",
    ""
};
char *NoNc[] = {
    "NO",
    "NC",
    ""
};
char *TimeMode[] = {
    "MANUAL",
    "NTP",
    ""
};
char *TimeZone[] = {
    "GMT-12",
    "GMT-11",        
    "GMT-10",                
    "GMT-9",
    "GMT-8",
    "GMT-7",
    "GMT-6",        
    "GMT-5",                
    "GMT-4",
    "GMT-3",
    "GMT-2",
    "GMT-1",        
    "GMT",                
    "GMT+1",
    "GMT+2",
    "GMT+3",
    "GMT+4",        
    "GMT+5",                
    "GMT+6",
    "GMT+7",
    "GMT+8",
    "GMT+9",        
    "GMT+10",
    "GMT+11",        
    "GMT+12",        
    "GMT+13",
    "GMT+14",
    ""
};
char *PanTilt[] = {
    "LEFT",
    "RIGHT",
    "UP",
    "DOWN",
    ""
};
char *Zoom[] = {
    "IN",
    "OUT",
    ""
};
char *Lens[] = {
    "MANUAL",
    "DCIRIS",
    ""
}; 
char *Exposure[] = {
    "AUTO",
    "MANUAL",
    ""
}; 
char *MShutter[] = {
    "4",
    "8",
    "15",
    "30",
    "60",
    "120",
    "250",
    "500",
    "1000",
    "2500",
    "5000",
    ""
}; 
char *Frequency[] = {
    "60HZ",
    "50HZ",
    ""
}; 
char *PtzProto[] = {
    "PELCO-D",
    ""
};
char *RecMode[] = {
    "VIDEO",
    "SNAPSHOT",
    ""
}; 
char *IdOd[] = {
    "INDOOR",
    "OUTDOOR",    
    ""
}; 
char *DayNight[] = {
    "AUTO",
    "COLOR",
    "BW",    
    ""
}; 
char *VideoType[] = {
    "NTSC",
    "PAL",
    ""
}; 
char *Language[] = {
    "ENGLISH",
    "CHINESE",
    ""
}; 

value_minmax_t value_minmax[] = {
    {"CH1BITRATE", 128, 8000},
    {"CH1MAXBITRATE", 128, 12000},
    {"CH2BITRATE", 128, 8000},
    {"CH2MAXBITRATE", 128, 12000},
    {"RS485ID", 0,15},
    {"BRIGHTNESS", 0, 255}, 
    {"CONTRAST",  0, 255}, 
    {"SHARPNESS",  0, 255},
    {"SATURATION",  0, 255},  
    {"IRISLEVEL", 0, 9},
    {"MANUALGAIN", 1, 255}, 
    {"UPDUR", 1, 1440}, 
    {"SDINTERVAL", 1, 10},
    {"MDINTERVAL",  1, 10},
    {"SNAPDUR", 10, 3600},  
    {"DODUR", 1, 30},
    {"SENSITIVITY", 1, 100},

    {"RTPPORT", 1, 65535},
    {"FTPPORT", 1, 65535},
    {"MAILPORT", 1, 65535},
    {"EVENTPORT", 1, 65535},
    {"SIZE", 1, 40000000},
    {"DEFAULT", 0, 1}, 
    {NULL, 0, 0}
};
long long GetDiskfreeSpace(const char *pDisk)
{
    long long int freespace = 0;    
    struct statfs disk_statfs;
    
    if( statfs(pDisk, &disk_statfs) >= 0 )
    {
        freespace = ((long long int)disk_statfs.f_bsize  * (long long int)disk_statfs.f_bfree);
    }

    return freespace;
}

int get_index(pvalue_t *vdata, char *name[])
{
    int i;
    int match = 0;
    char *target;
    target = vdata->value;
    for( i = 0; name[i] != NULL; i++){
        if( strcmp(target,name[i]) == 0 ){
            match = 1;
            break;
        }
    }
    if( match ){
        sprintf(vdata->sqlvalue,"%d",i);
        return i;
    } else {
        return ERR_NOT_SEARCH;
    }
}

int check_int_minmax(pvalue_t *vdata, value_minmax_t *v_minmax){

    int i;
    int match = 0;

    char *target_name = vdata->name;
    int target_value_int;
    
    target_value_int = atoi(vdata->value);
    for( i = 0; v_minmax[i].name != NULL; i++){
        if( strcmp(target_name, v_minmax[i].name) == 0 ){
            if(v_minmax[i].min <= target_value_int  && v_minmax[i].max >= target_value_int ){
                match = 1;
            }else{
                match = 0;
            }
            break;
        }
    }
    if( match ){
        return ERR_NOERROR;
    } else {
        return ERR_VALUE_INCONGRUITY;
    }    
}
int check_port(char *buf, char *name){
    
    return ERR_NOERROR;
}

int check_hostname(char *buf){
    char *regex = "(^[0-9a-zA-Z-]+(\\.[0-9a-zA-Z-]+)*$)";
    regex_t ext_regex;
    int ret;

    ret = regcomp(&ext_regex, regex, REG_EXTENDED);

    if(ret != 0){
        return ERR_VALUE_INCONGRUITY;
    }

    ret = regexec( &ext_regex, buf, 0, NULL, 0);
    regfree(&ext_regex);
    
    if(ret){
        return ERR_VALUE_INCONGRUITY;
    }
    return ERR_NOERROR;
}

int check_path(char *buf)
{
    char *regex =  "^[.]([/]([_0-9a-zA-Z-])+)+$|^([/]([_0-9a-zA-Z-])+)+$";
    regex_t ext_regex;
    int ret;

    if(!strcmp("./",buf) || !strcmp("/",buf)) return 1;
    
    ret = regcomp(&ext_regex, regex, REG_EXTENDED);

    if(ret != 0){
        return ERR_VALUE_INCONGRUITY;
    }

    ret = regexec( &ext_regex, buf, 0, NULL, 0);
    regfree(&ext_regex);
    if(ret){
        if(strstr(buf,"//")){
            return ERR_VALUE_INCONGRUITY;
        }
    }
    
    if(ret){
        return ERR_VALUE_INCONGRUITY;
    }
    
    return ERR_NOERROR;
}

int validate_ip_address(char *buf)
{
    char *regex =  "^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
    regex_t ext_regex;
    int ret;
    in_addr_t ipi;
    int iptop;
    
    ret = regcomp(&ext_regex, regex, REG_EXTENDED);

    if(ret != 0){
        return ERR_VALUE_INCONGRUITY;
    }

    ret = regexec( &ext_regex, buf, 0, NULL, 0);
    regfree(&ext_regex);
    
    if(ret){
        return ERR_VALUE_INCONGRUITY;
    }

    ipi = inet_addr(buf);
    if( ipi == -1 ) return ERR_VALUE_INCONGRUITY;

    iptop = ipi & 0x000000FF;

    if( iptop >= 224 || iptop == 0 || iptop == 127 )
        return ERR_VALUE_INCONGRUITY;
    
    return ERR_NOERROR;
} 

int validate_ipnetmask_address(char *buf)
{
    char *regex =  "^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
    regex_t ext_regex;
    int ret;
    in_addr_t ipi;
    printf("mask %s\n",buf);
    ret = regcomp(&ext_regex, regex, REG_EXTENDED);

    if(ret != 0){
        return ERR_VALUE_INCONGRUITY;
    }

    ret = regexec( &ext_regex, buf, 0, NULL, 0);
    regfree(&ext_regex);
    
    if(ret){
        return ERR_VALUE_INCONGRUITY;
    }
    printf("mask %s\n",buf);    
    ipi = inet_addr(buf);
    printf("ipi %08x, %08x\n",ipi,inet_addr("123.4.5.6"));
    printf("nm : %08x\n",inet_addr("255.255.0.0"));
    if( ipi == -1 || ipi == 0 ) return ERR_VALUE_INCONGRUITY;

    return ERR_NOERROR;
} 


char *intToBinary(int i) {
  static char s[8 + 1] = { '0', };
  int count = 8;

  do { s[--count] = '0' + (char) (i & 1);
       i = i >> 1;
  } while (count);

  return s;
}

int validate_netmask(char *buf){
    int ret;
    char *p;
    int number, i=0;
    char bin_str[32] = {0};
    char mask[32] = {0};
    int check_bin;
        
    ret = validate_ipnetmask_address(buf);
    if(ret){
        printf("return mask error\n");
        return -1;
    }    

//    char aaa[256];
//    char bbb[256];

//    sprintf(aaa,"%s", buf);
//    sprintf(bbb,".");
    strcpy(mask,buf);
    p =strtok(mask,".");
    number = atoi(p);
    strcpy(bin_str, intToBinary(number));
        
    while((p = strtok(NULL,".")) != NULL){
        number = atoi(p);
        strcat(bin_str, intToBinary(number));
    }

    if(bin_str[0] == '0'){
        return ERR_VALUE_INCONGRUITY;
    }
    
    check_bin = 0;
    for(i = 0; i < 32; i++){
        if(check_bin == 1){
            if(bin_str[i] == '1'){
                break;
            }
        }
        
        if(bin_str[i] == '0'){
            check_bin = 1;
        }
    }

    if(i < 31){
        return ERR_VALUE_INCONGRUITY;
    }
    
    return ERR_NOERROR;
}

int check_mail(char *buf)
{


//    char *regex = "(^[_0-9a-zA-Z-]+(\\.[_0-9a-zA-Z-]+)*@[0-9a-zA-Z-]+(\\.[0-9a-zA-Z-]+)*$)";
    char *regex = "^[0-9a-zA-Z]([-_\\.]?[0-9a-zA-Z])*@[0-9a-zA-Z]([-_\\.]?[0-9a-zA-Z])*\\.[a-zA-Z]{2,3}$";
    regex_t ext_regex;
    int ret;
    
    ret = regcomp(&ext_regex, regex, REG_EXTENDED);

    if(ret != 0){
        return ERR_VALUE_INCONGRUITY;
    }

    ret = regexec( &ext_regex, buf, 0, NULL, 0);
    regfree(&ext_regex);
    
    if(ret){
        return ERR_VALUE_INCONGRUITY;
    }
    return ERR_NOERROR;

}

int change_param(int type, pvalue_t *vdata)
{
    int idx = ERR_NOERROR;

    if(vdata->flag != 1){
        return idx;
    }
    
    switch( type ){
        case IT_INT :
            idx = check_int_minmax(vdata, value_minmax);
            if( idx == ERR_NOERROR ) strcpy(vdata->sqlvalue,vdata->value);

            break;
        case IT_STRING :
            strcpy(vdata->sqlvalue,vdata->value);
            
            break;
        case IT_ENFLAG :
            idx = get_index(vdata,EnFlag);
            break;
        case IT_ONFLAG :
            idx = get_index(vdata,OnFlag);
            break;
        case IT_VIDEOSIZE :
            idx = get_index(vdata,VideoSize);
            break;
        case IT_BAUDRATE:
            idx = get_index(vdata,BaudRate);
            break;
        case IT_CODEC:
            idx = get_index(vdata,Codec);
            break;
        case IT_RCTL:
            idx = get_index(vdata,Rctl);
            break;
        case IT_QUALITY:
            idx = get_index(vdata,Quality);
            break;
        case IT_FPS:
            idx = get_index(vdata,Fps);
            break;
        case IT_NETWORKMODE:
            idx = get_index(vdata,NetworkMode);
            break;
        case IT_LOWHIGH:
            idx = get_index(vdata,LowHigh);
            break;
        case IT_NONC:
            idx = get_index(vdata,NoNc);
            break;
        case IT_TIMEMODE:
            idx = get_index(vdata,TimeMode);
            break;
        case IT_TIMEZONE:
            idx = get_index(vdata,TimeZone);
            break;
        case IT_PANTILT:
            idx = get_index(vdata,PanTilt);
            break;
        case IT_ZOOM:
            idx = get_index(vdata,Zoom);
            break;
        case IT_LENS:
            idx = get_index(vdata,Lens);
            break;
        case IT_EXPOSURE:
            idx = get_index(vdata,Exposure);
            break;
        case IT_MSHUTTER:
            idx = get_index(vdata,MShutter);
            break;
        case IT_FREQUENCY:
            idx = get_index(vdata,Frequency);
            break;
        case IT_PTZPROTO:
            idx = get_index(vdata,PtzProto);
            break;
        case IT_RECMODE:
            idx = get_index(vdata,RecMode);
            break;
        case IT_DDNSPROVIDER:
            idx = get_index(vdata,DdnsProvider);
            break;
        case IT_RESOLUTION:
#if 0
            switch(sysresolution){
                case P_D1R : 
                    idx = get_index(vdata,ResolutionD1);
                    break;
                case P_720PR : 
                    idx = get_index(vdata,Resolution1080P);
                    if( idx == 0 ){
                        idx = 1;
                    }
                    break;
                case P_1080PR : 
                    idx = get_index(vdata,Resolution1080P);
                    break;
                case P_FISHEYE : 
                    idx = get_index(vdata,ResolutionFishEye);
                    break;                    
                default:
                    idx = get_index(vdata,Resolution1080P);
                    break;
            }
#endif
            break;
        case IT_IDOD:
            idx = get_index(vdata,IdOd);
            break;
        case IT_DAYNIGHT:
            idx = get_index(vdata,DayNight);
            break;
        case IT_VIDEOTYPE:
            idx = get_index(vdata,VideoType);
            break;
        case IT_LANGUAGE:
            idx = get_index(vdata,Language);
            break;
        case IT_PORT :
            idx = check_port(vdata->value, vdata->name);
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        case IT_EMAIL :
            idx = check_mail(vdata->value);
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        case IT_IPADDRESS :
            idx = validate_ip_address(vdata->value);
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        case IT_NETMASK :
            printf("initmask : %s\n",vdata->value);
            idx = validate_netmask(vdata->value);
            printf("itmask : %s\n",vdata->value);
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        case IT_HOSTNAME :
            idx = check_hostname(vdata->value);
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        case IT_SERVERADDR :
            idx = check_hostname(vdata->value);
            if(idx != ERR_NOERROR){
                idx = validate_ip_address(vdata->value);
            }
            strcpy(vdata->sqlvalue,vdata->value);
            break;
        default :
            idx = ERR_NOT_SEARCH;
            break;
    }
    return idx;
}

unsigned char get_checksum(char *buf, int size){
    int i;
    unsigned char checksum;

    checksum = 0;
    for(i = 0; i < size; i++){
        checksum += buf[i];
    }

    return checksum;
}

int touppercase(char *des,char *src)
{
    int i;
    for( i = 0; src[i] != 0; i++){
        des[i] = toupper(src[i]);
    }
    des[i] = 0;
    return 0;
}

int add_data(pkt_t *pkt, char *item,char *value)
{
    char buf[256];
    sprintf(buf,"%s=%s;",item,value);
    strcat((char *)pkt->data,buf);
    return 0;
}

int add_response(pkt_t *pkt, char *item)
{
    char buf[256];
    sprintf(buf,"%s;",item);
    strcat((char *)pkt->data,buf);
    return 0;
}

int mkpkthdr(pkt_t *pkt)
{
    pkt->cmdhdrsize = PKTHDRSIZE + strlen(pkt->phdr.cmdstr);
    pkt->totalsize = strlen((char *)pkt->data);
    sprintf(pkt->phdr.cmdsize,"%02d",pkt->cmdhdrsize - 2); // remove Len size
    sprintf(pkt->phdr.total_size,"%012d",pkt->totalsize);
    pkt->phdr.checksum = get_checksum(pkt->data,pkt->totalsize);
    return 0;
}

int mk_response(pkt_t *pkt, char *cmdstr, int error_flag)
{

    memset(pkt->data, 0, sizeof(pkt->data));    
    sprintf(pkt->phdr.cmdstr,"%s;",cmdstr);
    if( error_flag ){
        add_response(pkt,RSP_FAIL);        
    } else {
        add_response(pkt,RSP_SUCCESS);        
    }

    mkpkthdr(pkt);
    return 0;
}

int mk_response_msg(pkt_t *pkt, char *cmdstr, int error_flag, char *msg)
{
    int len;

    memset(pkt->data, 0, sizeof(pkt->data));    
    sprintf(pkt->phdr.cmdstr,"%s;",cmdstr);
    len = strlen(msg);

    if( error_flag ){
        add_response(pkt,RSP_FAIL);        
        if( len > 0 ) {
            add_response(pkt,msg);
        }
    } else {
        add_response(pkt,RSP_SUCCESS);        
        if( len > 0 ) {
            add_data(pkt,"DATA",msg);
        }
    }

    mkpkthdr(pkt);
    return 0;
}
int cam_filedownload(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr)
{
    int i;
    pvalue_t vdata[PO_DOWNLOADMAX];
    char buf[256];
    long long int freespace;
    int upgrade;
    char *p,*p1;

    upgrade = cam_client_get_upgrade(cam_client);
    if( upgrade != UPDATE_IDLE){
        mk_response_msg(pkt,cmdstr,1, "IN UPDATING PROCESS");
        logprt(LOG_INFO,"%s IN UPDATING PROCESS!", up->filename);
        return -1;
    } else {
        cam_client_set_upgrade(cam_client,UPDATE_FILESET);
        up->update = UPDATE_FILESET;
    }

    memset(vdata,0,sizeof(vdata));
    for( i = 0; i < PO_DOWNLOADMAX; i++){
        vdata[i].flag = get_str_data(pkt->data,CAMFileDownSet[i].item, vdata[i].value);
        logprt(LOG_DEBUG, "vdata %d, %s:%s",i,vdata[i].value,vdata[i].name);
    }
    for( i = 0; i < PO_DOWNLOADMAX; i++){
        vdata[i].name = CAMFileDownSet[i].item;
        change_param(CAMFileDownSet[i].type,&vdata[i]);
        logprt(LOG_DEBUG, "%d[%d] : %s = %s",i, vdata[i].flag, CAMFileDownSet[i].item, vdata[i].value);

    }
    strcpy(up->filename, vdata[PO_FILENAME].value);
    up->filesize = atoi(vdata[PO_FILESIZE].value);
    logprt(LOG_INFO,"filename: [%s], size : [%d]",up->filename,up->filesize);

    if( strstr(up->filename,"deb" ) != NULL ){
        up->type = UPTYP_KILROG;
    } else if( strstr(up->filename,"default.deb" ) != NULL ){
        up->type = UPTYP_KILROG;
    } else if( strstr(up->filename,"system.bin" ) != NULL ){
        up->type = UPTYP_SYSTEM;
    } else if( strstr(up->filename,".ubi" ) != NULL || strstr(up->filename,".bin" ) != NULL ){
        up->type = UPTYP_OPENWRT;
    } else if( strstr(up->filename,".tar.gz" ) != NULL ){
        up->type = UPTYP_OPENWRT_R;
    } else {
        mk_response_msg(pkt,cmdstr,1, "NOT SUPPORTED FILE");
        logprt(LOG_INFO,"%s NOT SUPPORTED FILE!", up->filename);
        return -1;
    }

    if (up->type != UPTYP_OPENWRT && up->type != UPTYP_OPENWRT_R){
        mk_response_msg(pkt,cmdstr,1, "NOT SUPPORTED FILE");
        logprt(LOG_INFO,"%s NOT SUPPORTED FILE!", up->filename);
        return -1;
    }

    freespace = GetDiskfreeSpace(OPENWRTDIR);

    logprt(LOG_INFO, "freespace : %lld", freespace);
    if( up->filesize * 1.5 > freespace ){
        mk_response_msg(pkt,cmdstr,1, "DISK SPACE FULL");
        logprt(LOG_INFO,"%s SIZE : %lld, DISK SPACE : %lld", up->filename, up->filesize, freespace);
        return -1;
    }

    logprt(LOG_INFO,"%s Download Start!", up->filename);    
    if (up->type == UPTYP_OPENWRT_R) logprt(LOG_DEBUG,"config restore file [%s]", up->filename);
    else logprt(LOG_DEBUG,"firmware file [%s]", up->filename);

    memset(buf,0,sizeof(buf));
    if( up->type == UPTYP_OPENWRT_R){
        sprintf(buf,"%s/restore.tar.gz",OPENWRTDIR);
    }
    else{
        sprintf(buf,"%s/firmware.img",OPENWRTDIR);
        // system("/usr/bin/killall dropbear uhttpd; sleep 1");
        // system("/bin/mount -t tmpfs -o remount,size=60m /tmp");
    }

    up->fp = fopen(buf,"wb");
    if( up->fp == NULL){
        logprt(LOG_INFO,"file open error",buf);
        mk_response_msg(pkt,cmdstr,1, "FILE CREATE ERROR");
        return -1;
    }

    mk_response(pkt,cmdstr,0);
    logprt(LOG_DEBUG,"cam_filedownload cmd end %s", up->filename);
    return 0;
}


int cam_downdata(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr)
{
    int flag;
    char str_seq[16] = {};
    int seq;
    char *p, *s;
    int size;
    int n;

    if( up->update == UPDATE_FILESET ){
        cam_client_set_upgrade(cam_client,UPDATE_DOWNDATA);
        up->update = UPDATE_DOWNDATA;
    } else if( up->update == UPDATE_DOWNDATA ){

    } else {
        mk_response_msg(pkt,cmdstr,1, "FILE NOT SPECIFIED");
        return -1;
    }

    if( up->fp == NULL ){
        mk_response_msg(pkt,cmdstr,1, "FILE NOT SPECIFIED");
        return -1;
    }

    flag = get_str_data(pkt->data,"SEQ",str_seq);
    seq = atoi(str_seq);
    if( up->seq == 0 ){
        up->seq = seq;
    } else if( seq == (up->seq + 1) ){
        up->seq = seq;
    } else {
        logprt(LOG_INFO,"sequence error in %d, rcv %d",up->seq,seq);
        mk_response_msg(pkt,cmdstr,1,"SEQUENCE NUMBER ERROR");
        return -2;

    }
    s = pkt->data;
    p = s + SIZE_SEQSIZE; // check sum
    size = pkt->totalsize - SIZE_SEQSIZE - 1 ;


    n = fwrite(p,1,size,up->fp);
    if( n != size ){
        logprt(LOG_INFO,"size error : n : %d, size : %d, cmdhdrsize : %d",n,size,pkt->cmdhdrsize);
        mk_response_msg(pkt,cmdstr,1,"FILE WRITE ERROR");
        return -1;
    }
    return 0;
}

int cam_upgrade(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *cmdstr)
{
    int i;
    pvalue_t vdata[PO_UPDATMAX];
    char filename[128];
    int  size;
    int  default_falg;
    char buf[128], buf2[128];
    
    if( up->update == UPDATE_DOWNDATA ){
        up->update = UPDATE_UPGRADE;
        cam_client_set_upgrade(cam_client,UPDATE_UPGRADE);
    } else {
        logprt(LOG_INFO,"upgrade status mismatch : %d",up->update);
        mk_response_msg(pkt,cmdstr,1,"FILE NOT SPECIFIED");
        return -1;
    }

    memset(vdata,0,sizeof(vdata));
    for( i = 0; i < PO_UPDATMAX; i++){
        vdata[i].flag = get_str_data(pkt->data,CAMUPDateSet[i].item, vdata[i].value);
    }
    
    for( i = 0; i < PO_UPDATMAX; i++){
        vdata[i].name = CAMUPDateSet[i].item;
        change_param(CAMUPDateSet[i].type,&vdata[i]);
        logprt(LOG_DEBUG,"%d : %s = %s",vdata[i].flag, CAMUPDateSet[i].item, vdata[i].value);
    }
    
    strcpy(filename, vdata[PO_FILENAME].value);
    size = atoi(vdata[PO_FILESIZE].value);
    default_falg = atoi(vdata[PO_DEFAULT].value);


    if( up->fp){
        fclose(up->fp);
        up->fp = NULL;
    }
    
    if( strcmp(up->filename,filename) != 0 ){
        logprt(LOG_INFO,"file differ");
        mk_response_msg(pkt,cmdstr,1,"FILE NAME DIFFERENT");
        return -1;
    }

    if( up->filesize != size ){
        logprt(LOG_INFO,"filesize differ");
        mk_response_msg(pkt,cmdstr,1,"FILE SIZE DIFFERENT");
        return -1;
    }

    cam_client_set_loadversion(cam_client,"NONE");
    if( up->type == UPTYP_SYSTEM ){
        sprintf(filename,"%s/Output_firmware",SYSTEMDIR);
        system("/usr/sbin/firmware_write");
        sprintf(buf,"%s-%s.system",up->platform,up->day);
    } else if( up->type == UPTYP_OPENWRT ){
        sprintf(filename,"%s/firmware.img",OPENWRTDIR);
        if(default_falg == 1){
            sprintf(buf2,"/sbin/sysupgrade -n %s",filename);        
        }else{
            sprintf(buf2,"/sbin/sysupgrade %s",filename);
        }
        system(buf2);
    } else if( up->type == UPTYP_OPENWRT_R ){
        sprintf(filename,"%s/restore.tar.gz",OPENWRTDIR);
        sprintf(buf2,"/sbin/sysupgrade -r %s; sync; /sbin/reboot &",filename);
        system(buf2);
    } else {
        sprintf(filename,"%s/%s",KILROGDIR,up->filename);
        sprintf(buf,"/usr/bin/dpkg -i %s",filename);
        system(buf);
        system("mount --bind /mnt/flash/etc /etc");
        system("mv /var/lib/dpkg/status /var/lib/dpkg/status.udeb.bak");
        system("touch /var/lib/dpkg/status");
        sprintf(buf,"%s-%s",up->platform,up->day);        
    }
    
    cam_client_set_loadversion(cam_client,buf);
//    sprintf(buf,"rm -rf %s",filename);
//    system(buf);
    mk_response(pkt,cmdstr,0);
    return 0;
}

int cam_upabort(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr)
{

    cam_client_upgrade_abort_all(cam_client);

    mk_response(pkt,cmdstr,0);

    return 0;
}

int cam_camversion(struct cam_client *cam_client, pkt_t *pkt, upgrade_t *up, char *mdstr)
{
    int ret=0;
   
    char loadversion[128];
    char flashversion[128];
    char createdate[128];    
    int  loadlen;
    int  flashlen;
    char line[128], *result;
    FILE *fp;

    memset(pkt->data, 0, sizeof(pkt->data));    
    sprintf(pkt->phdr.cmdstr,"%s;",pkt->phdr.cmdstr);

    memset(loadversion,0,sizeof(loadversion));
    memset(flashversion,0,sizeof(flashversion));    

    pkt->error_flag = 0;

    cam_client_get_loadversion(cam_client,loadversion);

    memset(line,0,sizeof(line));
    fp = fopen(FLASHFILE,"r");
    if(fp){
        while (fgets(line, sizeof(line), fp) != NULL) {
            if (strncmp(line, "DISTRIB_REVISION", 16) == 0) {
                result = strtok(line, "'");
                if (result != NULL) {
                    result = strtok(NULL, "'");
                    strcpy(flashversion, result);
                }
            }
        }
        fclose(fp);
    }

    loadlen = strlen(loadversion) ;
    flashlen = strlen(flashversion);
    loadversion[loadlen] = 0;
    flashversion[flashlen] = 0;

    if( strncmp(flashversion,"FLASHERASED",11) == 0){
        add_response(pkt,RSP_FAIL);
    } else {
        add_response(pkt,RSP_SUCCESS);
    }

    add_data(pkt,"LOADVERSION",loadversion);
    add_data(pkt,"FLASHVERSION",flashversion);

    mkpkthdr(pkt);

    return ret;
}

int cam_reboot(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr)
{
    int ret = 0;
    mk_response(pkt, cmdstr,0);
    logprt(LOG_INFO,"System Reboot!");

    system("sync; sync; /sbin/reboot &");
    return ret;

}
int cam_harddefault(struct cam_client *cam_client, pkt_t *pkt, char *cmdstr)
{
    int ret = 0;
    mk_response(pkt, cmdstr,0);
    logprt(LOG_INFO,"System HardDefault!");

    system("sync;sync;echo '7\n' > /tmp/sys_mgr.fifo &");
    return ret;

}


