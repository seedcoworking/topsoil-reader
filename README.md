# Topsoil Reader

Topsoil Reader is a Raspberry Pi based system that controls RFID access to Seed Coworking.

## Where are the project files?!* 
Well, they got deleted. We're starting from scratch here.
If you want to know why, go to the **deaduino** branch. We kept it for historical reasons.

## So, what now?

### First, we do full TDD - hardware included
This means we need to emulate the HID6005 Weigand RFID scanner... with a Netduino. This will involve a bit of solder as well as a fair amount of code. 

### Next, hardware design
This will include a Raspberry Pi, a custom adapter card with ATtiny and 22v10, and a physcal interface for control and debug. 

### Then, lots of code
Let's see... CUPL, C, C++, Ruby, possibly Python or PHP, and maybe some more C#.
Not sure about the last three, it depends on what needs done and what fits best.

### Finally, there is no finally
We'll put it into service when it works reliably, then continue to add new features as people think them up.

## Hardware

### Door Hardware

**Door strike**

This door strike was recommended by McElheney:
[http://www.vonduprin.com/pdf/VonDup_5100_elec.strike.pdf](http://www.vonduprin.com/pdf/VonDup_5100_elec.strike.pdf)

**Touchbar**

This touch bar was recommended by McElheney:
[http://exits.doromatic.com/pdf/installation/devices/ININST_1002_G.pdf](http://exits.doromatic.com/pdf/installation/devices/ININST_1002_G.pdf)
