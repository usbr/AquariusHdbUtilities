# AquariusHdbUtilities
This codebase contains the utilities used by the Boulder Canyon Operations Office in transferring, synchronizing, and managing data workflows between HDB and Aquarius. The program is a command line program that accepts different input parameters to perform different data transfer and data manipulation functions and is run by a Windows Task Scheduler on the Aquarius application server. This program is only configured to work for the specific and customized implementation of Aquarius within the Boulder Canyon Operations Office.

This codbase requires the following references and packages:
* [RestSharp](https://github.com/restsharp/RestSharp)
* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
* [Reclamation.TimeSeries](https://github.com/usbr/Pisces)
