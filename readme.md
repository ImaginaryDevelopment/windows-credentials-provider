
# Windows Credential Provider
_Made with C# and F#, .NET_

This is tested as working in windows 10

The code is totally free for any use.

## *Read this before you start*

Installing an untested credential provider might lock you out of the system,
as the code will run in process with winlogon.

Use a live distro to remove the dll if that happens.

Better yet, use a VM to do your experiments.

_Consider yourself warned._

## Installation
To start a setup to develop your own Windows Credential Provider:
- Install the COM component by building the project
- Merge the registry to install the cred. provider

The projects are setup for x64 systems - you might need to change that if you want it to run on 32bit platforms. Same goes for registry installation.

When you run TestConsoleApp you should be able to see your provider under "more choices" (windows 10).

## Generating a regfile

`regasm my.dll /regfile`

## May have also needed

New-ItemProperty -Path "HKLM:\SOFTWARE\MICROSOFT\Windows\CurrentVersion\Policies\System\" -Name "DontDisplayLastUserName" -PropertyType DWord -Value 1 -Force

## What it can do
It connects the logon procedure with alternative means to logon, like images from cameras, voices with microphone.

## More info
I have included the official doc on how to use the credential provider - note that you have to have some knowledge about COM and the examples are in C++.

I have also included the guide on how to (re)export Interop typelib from IDL in windows SDK. You can use that to export almost any component.

## In use file locations

When the provider is in use it may involve the following files

 - "C:\Windows\System32\CredentialProviderLog.log.txt"
