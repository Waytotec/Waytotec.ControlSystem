#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <uci.h>

#include "uci_conf.h"

static struct uci_context *context = NULL;

static char* uci_show_value(struct uci_option *o)
{
    struct uci_element *e;
    bool sep = false;
    char *ret_value = NULL;
    
    switch(o->type) {
    case UCI_TYPE_STRING:
//        printf("%s\n", o->v.string);
        ret_value = o->v.string;
        break;
    case UCI_TYPE_LIST:
        uci_foreach_element(&o->v.list, e) {
//            printf("\n%s==%s\n", (sep ? delimiter : ""), e->name);
            if(!sep){
                ret_value = e->name;
            }
            sep = true;
        }
//        printf("\n");
        break;
    default:
        printf("<unknown>\n");
        break;
    }
    return ret_value;
}


char *conf_get(char *key)
{
    struct uci_ptr ptr = { 0 };
    char str[1024];

    sprintf(str, "%s", key);
    if (uci_lookup_ptr(context, &ptr, str, true) != UCI_OK)
        return NULL;
        
    if (!(ptr.flags & UCI_LOOKUP_COMPLETE))
        return NULL;

    return uci_show_value(ptr.o);
}

int conf_set(char *key, char *val)
{
    struct uci_ptr ptr = { 0 };
    char str[1024];

    sprintf(str, "%s=%s", key, val);
    if (uci_lookup_ptr(context, &ptr, str, true) != UCI_OK)
        return -1;

    return uci_set(context, &ptr);
}

int conf_load(char *name)
{
    struct uci_package *p;

    p = uci_lookup_package(context, name);
    if (p)
        uci_unload(context, p);

    if (uci_load(context, name, &p))
        return -1;
    return 0;
}

int conf_save(char *name)
{
    struct uci_package *p;

    p = uci_lookup_package(context, name);
    if (!p)
        return -1;

    if (uci_save(context, p))
        return -1;

    if (uci_commit(context, &p, false))
        return -1;
    return 0;
}

int conf_init(char *name)
{
    context = uci_alloc_context();
    if (!context) {
        return -1;
    }
    context->flags &= ~UCI_FLAG_STRICT;

    if (name)
        return conf_load(name);
    return 0;
}

void conf_exit(void)
{
    if (context)
        uci_free_context(context);
}

