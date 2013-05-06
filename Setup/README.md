# Kudu Dev Setup Scripts
In this folder is a simple script and custom WebPi feed that does most of the steps outlined 
in [Getting started](https://github.com/projectkudu/kudu/wiki/Getting-started)
it should install all the prerequisites, but there still a few manual steps required to
deal with in order to run the functional tests. What is missing are the steps needed
for getting VS2010 project files, Mercurial, and XUnit runners.

## Usage
Simply run the script as Admin, and accept the EULAs. Reboots are suppressed, so 
it probably a good idea to reboot after the install completes. 

## Known Issues
Currently, the script install enable .NET3.5 which is likely an unneeded dependency, I will
have to track down what is pulling this in and remove that dependnecy. 