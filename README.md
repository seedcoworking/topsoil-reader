# Topsoil Reader - *deaduino* -goto>> [master](https://github.com/seedcoworking/topsoil-reader/edit/master) or [development](https://github.com/seedcoworking/topsoil-reader/edit/development)

Topsoil Reader is a Netdunio application written to control RFID access to Seed Coworking.
See the [master](https://github.com/seedcoworking/topsoil-reader/edit/master) or [development](https://github.com/seedcoworking/topsoil-reader/edit/development) branches for current code and documentation. 
This branch is called **deaduino** for a reason.

This branch of the RFID reader has been discontinued due to hardware implementation issues and is being replaced by a more reliable system. The main problem is that netduino can't use the network and read tags at the same time without having a stroke.

I have plans to make this work eventually by modifying the firmware,writing a proper WiFly driver, and re-porting NETMF. But for now, it is not reliable enough to be used in a real-world situation.

Check my [Github Page](https://github.com/testmonkey107) for future updates on NETMF

## Hardware

### Door Hardware

**Door strike**

This door strike was recommended by McElheney:
[http://www.vonduprin.com/pdf/VonDup_5100_elec.strike.pdf](http://www.vonduprin.com/pdf/VonDup_5100_elec.strike.pdf)

**Touchbar**

This touch bar was recommended by McElheney:
[http://exits.doromatic.com/pdf/installation/devices/ININST_1002_G.pdf](http://exits.doromatic.com/pdf/installation/devices/ININST_1002_G.pdf)
