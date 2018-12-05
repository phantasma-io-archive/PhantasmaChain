using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System;
using System.Linq;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void TestDecimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            Assert.IsTrue(n == TokenUtils.ToBigInteger(TokenUtils.ToDecimal(n, places), places));
            Assert.IsTrue(d == TokenUtils.ToDecimal(TokenUtils.ToBigInteger(d, places), places));

            Assert.IsTrue(d == TokenUtils.ToDecimal(n, places));
            Assert.IsTrue(n == TokenUtils.ToBigInteger(d, places));
        }

        [TestMethod]
        public void TestNexus()
        {
            var owner = KeyPair.Generate();
            var nexus = new Nexus("tests", owner);

            var rootChain = nexus.RootChain;
            var token = nexus.NativeToken;

            Assert.IsTrue(token != null);
            Assert.IsTrue(token.CurrentSupply > 0);
            Assert.IsTrue(token.MaxSupply > 0);

            Assert.IsTrue(rootChain != null);
            Assert.IsTrue(rootChain.BlockHeight > 0);
            Assert.IsTrue(rootChain.ChildChains.Any());

            var txCount = nexus.GetTotalTransactionCount();
            Assert.IsTrue(txCount > 0);

            /*
            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            var tx = new Transaction(ScriptUtils.TokenTransferScript(chain, token, owner.Address, third.Address, 5), 0, 0);
            tx.Sign(owner);
            */
            /*var block = ProofOfWork.MineBlock(chain, miner.Address, new List<Transaction>() { tx });
            chain.AddBlock(block);*/
        }

        [TestMethod]
        public void TestTokenTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var token = nexus.NativeToken;

            var testUser = KeyPair.Generate();

            var amount = TokenUtils.ToBigInteger(400, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void TestAccountRegister()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var token = nexus.NativeToken;

            Func<KeyPair, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    result = simulator.EndBlock();

                    if (result)
                    {
                        Assert.IsTrue(tx != null);

                        var lastBlock = nexus.RootChain.LastBlock;
                        var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                        Assert.IsTrue(evts.Any(x => x.Kind == Blockchain.Contracts.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var testUser = KeyPair.Generate();

            var amount = TokenUtils.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(balance == amount);

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
            Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
            Assert.IsFalse(registerName(testUser, targetName+"!"));
            Assert.IsTrue(registerName(testUser, targetName));

            var currentName = nexus.LookUpAddress(testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            Assert.IsFalse(registerName(testUser, "other"));
        }

        [TestMethod]
        public void TestInterchainTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var amount = TokenUtils.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, receiver.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == amount);



            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, token, sourceChain, receiver.Address, targetChain, TokenUtils.ToBigInteger(10, token.Decimals));
            //var script = ScriptUtils.CallContractScript("token", "SendTokens", targetChain.Address, owner.Address, receiver.Address, token.Symbol, amount);
            //simulator.MakeTransaction(owner, sourceChain, script);

            simulator.EndBlock();
            var blockA = nexus.RootChain.LastBlock;


            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock());

            // verify balances
            balance = targetChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == amount);

            balance = sourceChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == 0);

        }

        [TestMethod]
        public void TestInterchainTransferSameAccount()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = sender;

            var amount = TokenUtils.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, receiver.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == amount);


            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, token, sourceChain, receiver.Address, targetChain, TokenUtils.ToBigInteger(10, token.Decimals));
            //var script = ScriptUtils.CallContractScript("token", "SendTokens", targetChain.Address, owner.Address, receiver.Address, token.Symbol, amount);
            //simulator.MakeTransaction(owner, sourceChain, script);

            simulator.EndBlock();
            var blockA = nexus.RootChain.LastBlock;


            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock());

            // verify balances
            balance = targetChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == amount);

            balance = sourceChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == 0);

        }

        [TestMethod]
        public void TestNftMint()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftKey = KeyPair.Generate();
            var nftSymbol = "LT";

            var testUser = KeyPair.Generate();

            // Create the token LamboToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "LamboToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a LamboToken?");

            // Mint a new LamboToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(testUser, nftKey.Address, chain, token, tokenData);
            simulator.EndBlock();

            // verify nft presence on the user post-mint
            ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = chain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");
        }

        
        [TestMethod]
        public void TestNftBurn()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftKey = KeyPair.Generate();
            var nftSymbol = "LT";

            var testUser = KeyPair.Generate();

            // Create the token LamboToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "LamboToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a LamboToken?");

            // Mint a new LamboToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(testUser, nftKey.Address, chain, token, tokenData);
            simulator.EndBlock();

            // verify nft presence on the user post-mint
            ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the user not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = chain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // burn the token
            simulator.BeginBlock();
            simulator.GenerateNftBurn(testUser, chain, token, tokenId);
            simulator.EndBlock();

            //verify the user no longer has the token
            ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user still have it post-burn?");
        }
        
        [TestMethod]
        public void TestNftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftKey = KeyPair.Generate();
            var nftSymbol = "LT";
            var nftName = "LamboToken";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            // Create the token LamboToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, nftName, 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a LamboToken?");

            // Mint a new LamboToken directly on the sender
            simulator.BeginBlock();
            simulator.GenerateNft(sender, nftKey.Address, chain, token, tokenData);
            simulator.EndBlock();

            // verify nft presence on the sender post-mint
            ownedTokenList = chain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            
            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = chain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = chain.GetTokenOwnerships(token).Get(receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a LamboToken?");

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            var txA = simulator.GenerateNftTransfer(sender, receiver.Address, chain, token, tokenId);
            simulator.EndBlock();

            // verify nft presence on the receiver post-transfer
            ownedTokenList = chain.GetTokenOwnerships(token).Get(receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = chain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void TestSidechainNftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var nftKey = KeyPair.Generate();
            var nftSymbol = "LT";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            // Create the token LamboToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "LamboToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownedTokenList = sourceChain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a LamboToken?");

            // Mint a new LamboToken directly on the sender
            simulator.BeginBlock();
            simulator.GenerateNft(sender, nftKey.Address, sourceChain, token, tokenData);
            simulator.EndBlock();

            // verify nft presence on the sender post-mint
            ownedTokenList = sourceChain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = sourceChain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = targetChain.GetTokenOwnerships(token).Get(receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a LamboToken?");
            

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            var txA = simulator.GenerateNftSidechainTransfer(sender, receiver.Address, sourceChain, targetChain, token, tokenId);
            simulator.EndBlock();

            // verify the sender no longer has it
            ownedTokenList = sourceChain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

            // verify nft presence on the receiver post-transfer
            ownedTokenList = targetChain.GetTokenOwnerships(token).Get(receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = sourceChain.GetNFT(token, tokenId);
            Assert.IsTrue(nft.ReadOnlyData.SequenceEqual(tokenData) || nft.DynamicData.SequenceEqual(tokenData),
                "And why is this NFT different than expected? Not the same data");
        }

    }
}
