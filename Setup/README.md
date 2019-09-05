# Kudu Dev Setup Scripts
In this folder is a simple script and custom WebPI feed that does most of the steps outlined
in [Getting started](https://github.com/projectkudu/kudu/wiki/Getting-started)
it should install all the prerequisites, need to run the basic Kudu scenarios.
Running Functional tests still require some manual steps see Known issue sections below.
## Usage
Simply run the script as Admin, and accept the EULAs. Reboots are suppressed, so
it probably a good idea to reboot after the install completes.

if your machine does not have any visual studio 2013 installed, KuduDevSetup.cmd will quietly install visual studio 2013 community edition. If you would like your own choice of visual studio, please install visual studio before runing setup script.

## Known Issues
There still are a few manual steps required in order to run the functional tests. What is
missing is getting XUnit. This has not been
tested on anything but Windows 8 and Server 2012 x64. You may run into issues
on other OSes architectures, if you do please report them.

On a computer without .NET 3.5 installed the custom feed may enable .NET3.5
this is not a hard dependency of Kudu, but a consequence of using
some of the standard WebPI product dependencies to enable .NET4.5 extensibility for IIS.
