# SeedPi

SeedPi is a PHP proxy that runs on RaspberryPi and allows the netduino controller to get updated scheduling info. 
It should Be run over a dedicated peer-to-peer ethernet cable because the netduino can't handle SSL.
A second connection should use wifi and https to connect to the topsoil server.

This project will probably see a few more tweaks, but it will be replaced by mud-pi eventually.

## Here are the instructions for setting this up on raspi
### Hardware
Raspi RevB
4G sd card and reader
usb wifi
Ethernet cable
Usb keyboard/mouse combo
TV or Monitor with hdmi input
hdmi cable
1A usb power supply

### Install OS
get the latest Raspbian from [raspberrypi.org](http://www.raspberrypi.org/downloads/)

follow the [Install Instructions at raspberrypi.org](http://www.raspberrypi.org/documentation/installation/installing-images/README.md)

insert the sd card, wifi, kybd/mouse, and hdmi into the raspi.

Apply power

### Initial Setup
change the pi password
verify wifi operation and set appropriate settings for your network
Get the IPv4 address of the raspi
verify ssh is turned on by logging in from another computer
Remove monitor, keyboard and mouse since they are no longer needed.

### add users

### Configure Static IP on NIC

### install web server (Static IP Only)

### Set up crontab

### Install doordat.php

### Edit doorconf.php

### Test seed-pi from PC




