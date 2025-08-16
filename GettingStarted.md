# Getting Started

This document will cover the tools you need and how to setup this project locally to build and debug

## Prerequisites

1. Windows Desktop.
1. Visual Studio 2022 +
    1. .net 8 +
    1. Asp.net workload

if you also want to deploy and use the Azure Bot Service Channel System, you will further need:

1. Ability to create Entra ID application identities
1. Ability to create Azure Bot Service Resources
1. Ability to deploy App Services and or Containers
1. Additionally Teams deployments requires ability to create and deploy teams apps.

This is a general list only, Each Sample has a more detailed ReadMe.md that details the specific requirements of the sample.

## Clone and build

1. Clone the repro to your development workstation.
1. Open the src\Microsoft.Agents.SDK.sln project using Visual Studio.
1. Build

This should restore and build the Framework.

You can then use the Test Explore to run tests as needed.
