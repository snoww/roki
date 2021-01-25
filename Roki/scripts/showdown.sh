#!/usr/bin/env bash

# second argument: the user to accept the challenge
/home/snow/Documents/showdown/run.py $2 &
sleep 2
# first argument: the user issuing challenge
/home/snow/Documents/showdown/run.py $1 &