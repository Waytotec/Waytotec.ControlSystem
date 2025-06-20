#!/bin/sh

LOG_FILE1="/mnt/free0.log"
BACKUP_FILE1="/mnt/free1.log"
MAX_LINES1=10000
LOG_FILE2="/mnt/top0.log"
BACKUP_FILE2="/mnt/top1.log"
MAX_LINES2=20000
LOG_FILE3="/mnt/ifconfig0.log"
BACKUP_FILE3="/mnt/ifconfig1.log"
MAX_LINES3=20000

while true
do
  echo "---- $(date) ----" >> $LOG_FILE1
  free >> $LOG_FILE1

  LINE_COUNT=$(wc -l < $LOG_FILE1)

  if [ "$LINE_COUNT" -gt "$MAX_LINES1" ]; then
    mv $LOG_FILE1 $BACKUP_FILE1
    touch $LOG_FILE1
  fi

  echo "---- $(date) ----" >> $LOG_FILE2
  top -b -n 1 | head -n 20 >> $LOG_FILE2

  LINE_COUNT=$(wc -l < $LOG_FILE2)

  if [ "$LINE_COUNT" -gt "$MAX_LINES2" ]; then
    mv $LOG_FILE2 $BACKUP_FILE2
    touch $LOG_FILE2
  fi

  echo "---- $(date) ----" >> $LOG_FILE3
  ifconfig >> $LOG_FILE3

  LINE_COUNT=$(wc -l < $LOG_FILE3)

  if [ "$LINE_COUNT" -gt "$MAX_LINES3" ]; then
    mv $LOG_FILE3 $BACKUP_FILE3
    touch $LOG_FILE3
  fi

  sleep 600
done
