<p align="center">
  <img
    src="https://res.cloudinary.com/dqnpej4fo/image/upload/v1528636880/phantasma_logo.svg"
    width="125px"
  >
</p>

<h1 align="center">Phantasma</h1>

<p align="center">
  Decentralized network for smart storage
</p>

## Contents

- [Description](#description)
- [Components](#components)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Building](#building)
- [Contributing](#contributing)
- [TODO](#todo)
- [Credits and License](#credits-and-license)

---

## Description

Phantasma implements a decentralized content distribution system running on the blockchain, with strong emphasis on privacy and security.

To learn more about Phantasma, please read the [White Paper](https://phantasma.io/phantasma_whitepaper.pdf).

## Components

Component 		| Description	| Status
:---------------------- | :------------
Chain Core 		| eg: accounts, transactions, blocks | Done
VM 		| Virtual machine to run smart contracts | In development
Smart Contracts | eg: language, features, compilers | In development
Network 			| P2P communication | R&D
Consensus | Distributed consensus for nodes | R&D
API 			| RPC api for nodes | Planned

## Compatibility

Platform 		| Status
:---------------------- | :------------
.NET Framework 		| Working
.NET Core 		| Untested
UWP 			| Untested
Mono 			| Untested
Xamarin / Mobile 	| Untested
Unity 			| Untested

## Installation

To install Phantasma SDK to your project, run the following command in the [Package Manager Console](https://docs.nuget.org/ndocs/tools/package-manager-console):

```
PM> Install-Package Phantasma
```

## Building

To build Phantasma on Windows, you need to download [Visual Studio 2017](https://www.visualstudio.com/products/visual-studio-community-vs), install the [.NET Framework 4.7 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=55168) and the [.NET Core SDK](https://www.microsoft.com/net/core).

If you need to develop on Linux or macOS, just install the [.NET Core SDK](https://www.microsoft.com/net/core).

For more information about how to build dApps for Phantasma, please read the [documentation](http://phantasma.io/development),

## Contributing

You can contribute to Phantasma with [issues](https://github.com/PhantasmaProtocol/PhantasmaChain/issues) and [PRs](https://github.com/PhantasmaProtocol/PhantasmaChain/pulls). Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.


## License

The Phantasma project is released under the MIT license, see `LICENSE.md` for more details.