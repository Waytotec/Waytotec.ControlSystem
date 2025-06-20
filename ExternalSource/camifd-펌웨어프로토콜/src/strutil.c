#include <stdio.h>
#include <stdlib.h>
#include <string.h>
int str_fine(char *data,char sch, int endsize)
{
    int i;
    for( i = 0; data[i] && i < endsize ; i++){
        if(data[i] == sch) break;
    }
    return i;
}

int get_str_data(char *data_buf, char *sch_str, char *target){
    int lensize;
    int strindex;
    char *str_s;
    char *str_index;

    str_s = strstr(data_buf, sch_str);
    if(str_s != NULL){
        lensize = strlen(sch_str) + 1;
        str_index = str_s + lensize;
        strindex = str_fine(str_index, ';', 2048);
        strncpy(target, str_index, strindex);
    }else{
        return 0;
    }    
    return 1;
}
