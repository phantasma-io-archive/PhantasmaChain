using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Phantasma.Blockchain.Storage;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Blockchain;

namespace Phantasma.Tests
{
    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void TestMarketContract()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 1000000);
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(owner, testUser.Address, chain, token, tokenROM, tokenRAM);
            simulator.EndBlock();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var price = 1000;

            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(2);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "SellToken", testUser.Address, token.Symbol, Nexus.FuelTokenSymbol, tokenID, price, endDate).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BuyToken", owner.Address, token.Symbol, auctions[previousAuctionCount].TokenID).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");
        }

        [TestMethod]
        public void TestFriendsContract()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var testUser = KeyPair.Generate();
            var secondUser = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 1000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
            {
                return ScriptUtils
                   .BeginScript()
                   .AllowGas(testUser.Address, Address.Null, 1, 9999)
                   .CallContract("friends", "AddFriend", testUser.Address, secondUser.Address)
                   .SpendGas(testUser.Address)
                   .EndScript();
            });
            simulator.EndBlock();

            var friends = (Address[])simulator.Nexus.RootChain.InvokeContract("friends", "GetFriends", testUser.Address);
            Assert.IsTrue(friends.Length == 1);
            Assert.IsTrue(friends[0] == secondUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
            {
                return ScriptUtils
                   .BeginScript()
                   .AllowGas(testUser.Address, Address.Null, 1, 9999)
                   .CallContract("friends", "RemoveFriend", testUser.Address, secondUser.Address)
                   .SpendGas(testUser.Address)
                   .EndScript();
            });
            simulator.EndBlock();

            friends = (Address[])simulator.Nexus.RootChain.InvokeContract("friends", "GetFriends", testUser.Address);
            Assert.IsTrue(friends.Length == 0);
        }
    }
}
