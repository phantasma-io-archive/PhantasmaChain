<p align="center">
  <img
    src="https://phantasma.io/img/phantasma_color.png"
    width="125px"
  >
</p>

<h1 align="center">Phantasma</h1>

<p align="center">
  Decentralized network for smart storage
</p>

<p align="center">      
  <a href="https://travis-ci.org/phantasma-io/PhantasmaChain">
    <img src="https://travis-ci.org/phantasma-io/PhantasmaChain.svg?branch=master">
  </a>
  <a href="https://github.com/phantasma-io/PhantasmaChain/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg">
  </a>

</p>

## Contents

- [Description](#description)
- [Components](#components)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Building](#building)
- [Contributing](#contributing)
- [License](#license)

---

## Description

Phantasma implements a decentralized content distribution system running on the blockchain, with strong emphasis on privacy and security.

To learn more about Phantasma, please read the [White Paper](https://phantasma.io/phantasma_whitepaper.pdf).

## Components

Component	| Description	| Status	| Percentage
:---------------------- | :------------ | :------------  | :------------ 
Chain Core 		| eg: accounts, transactions, blocks | In development | 80%
Wallet | CLI wallet | In development | 30%
VM 		| Virtual machine to run smart contracts | In development | 60%
Smart Contracts | eg: language features, compilers | In development | 30%
Economy | Tokens / NFT | In development | 70%
Network 			| P2P communication | In development | 60%
Consensus | Distributed consensus for nodes | In development | 20%
Scalabilty | Side-chains / Channels | In development | 60%
Relay | Off-chain relay | In development | 10%
Storage | Distributed storage | In development| 40%
API 			| RPC api for nodes | In development | 40%

## Compatibility

Platform 		| Status
:---------------------- | :------------
.NET Framework 		| Working
.NET Core 		| Working
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

For more information about how to build dApps for Phantasma, please read the [documentation](http://phantasma.io/development).

## Contributing

You can contribute to Phantasma with [issues](https://github.com/PhantasmaProtocol/PhantasmaChain/issues) and [PRs](https://github.com/PhantasmaProtocol/PhantasmaChain/pulls). Simply filing issues for problems you encounter is a great way to contribute. Contributing implementations is greatly appreciated.


## License

The Phantasma project is released under the MIT license, see `LICENSE.md` for more details.