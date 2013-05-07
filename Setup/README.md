# Kudu Dev Setup Scripts
In this folder is a simple script and custom WebPI feed that does most of the steps outlined
in [Getting started](https://github.com/projectkudu/kudu/wiki/Getting-started)
it should install all the prerequisites, need to run the basic Kudu scenarios.
Running Functional tests still require some manual steps see Known issue sections below.
## Usage
Simply run the script as Admin, and accept the EULAs. Reboots are suppressed, so
it probably a good idea to reboot after the install completes.

## Known Issues
There still a few manual steps required in order to run the functional tests. What is
missing is getting VS2010 project files and XUnit. This has not been
tested on anything but Windows 8 and Server 2012 x64. You may run into issues
on other OSes architectures. If so feel free to report them.

On a computer without .NET 3.5 installed it the custom feed may enable .NET3.5
this is not a hard dependency as far as we know, but a consequence of using
some of the standard WebPI product dependencies to enable .NET4.5 extensibility.
