
/***************************************************************************
 *  - Limits and defines
 ***************************************************************************/

#define IP_ADDRESS_MAX_LEN      (16) /* 15 for "255.255.255.255" + 1 for
                                      * null-terminator */
#define MAC_ADDRESS_MAX_LEN     (18) /* 17 for "00:80:AD:40:D8:79" + 1 for
                                      * null-terminator */
#define HOSTNAME_MAX_LEN        (300) /* FIXME, what is the maximum? */

#define SERIALIF				"eth0"


/***************************************************************************
 *  - Data structures
 ***************************************************************************/
/* RTA-friendly data-structure for network interfaces */
typedef struct Interface
{
    char     name[IFNAMSIZ];
    int      kidx;       /* index of interface in kernel */
    int      status;     /* Readable only. 0 for down, otherwise up */
    char     IpAddress[IP_ADDRESS_MAX_LEN];
    char     Broadcast[IP_ADDRESS_MAX_LEN];
    char     Mask[IP_ADDRESS_MAX_LEN];
    char     MacAddress[MAC_ADDRESS_MAX_LEN];  
    struct route *staticRoutes; /* Not visible through RTA */
    struct Interface *next;
}
Interface;
    


/* RTA-friendly data-structure for miscellaneous host information */
typedef struct HostRow
{
  char     hostname[HOSTNAME_MAX_LEN];
  char     gateway[IP_ADDRESS_MAX_LEN];
}
HostRow;

