# Wiegand Simulator

Netduino based Weigand format RFID simulator for testing a reader interface.  
Designed to simulate the HID 6005 RFID reader in use at Seed.

## Features

2 wire weigand ouput with on-board test loopback and text output  
red/green LED inputs routed to LED and text outputs  
beeper input routed to piezo and text output  
DC load test 20mA with 80mA peaks  
serial connection for automation and debug output  

## Simulation

send weigand codes up to 80-bits with parity on each end  
send corrupted codes or incorrect parity bits  
create fault conditions at alarm and LED inputs  
create overcurrent and open load conditions  

