#!/bin/sh
DEFAULTDIR=/etc/defaultconf
CONFIGDIR=/etc/config
logger "Factory Default"
#eepromutil -e 0 -u slm -p ktseoul --------> ??????????
sync

if [$1 -eq "SOFT"] ; then
	cp $CONFIGDIR/network /tmp/network
#	rm -f $CONFIGDIR/*
	cp -a $DEFAULTDIR/* $CONFIGDIR/
	rm /tmp/network
else
#	rm -f $CONFIGDIR/*
	cp -a $DEFAULTDIR/* $CONFIGDIR/
fi

sync
sync

exit 0
