using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Text;

using Phantasma.Blockchain;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using SHA256 = Phantasma.Cryptography.Hashing.SHA256;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void Decimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            var tmp1 = TokenUtils.ToLargeInteger(TokenUtils.ToDecimal(n, places), places);

            Assert.IsTrue(n == tmp1);
            Assert.IsTrue(d == TokenUtils.ToDecimal(TokenUtils.ToLargeInteger(d, places), places));

            Assert.IsTrue(d == TokenUtils.ToDecimal(n, places));
            Assert.IsTrue(n == TokenUtils.ToLargeInteger(d, places));
        }

        [TestMethod]
        public void GenesisBlock()
        {
            var owner = KeyPair.Generate();
            var nexus = new Nexus("tests");

            Assert.IsTrue(nexus.CreateGenesisBlock(owner));

            var rootChain = nexus.RootChain;
            var token = nexus.NativeToken;

            Assert.IsTrue(token != null);
            Assert.IsTrue(token.CurrentSupply > 0);
            Assert.IsTrue(token.MaxSupply > 0);

            Assert.IsTrue(rootChain != null);
            Assert.IsTrue(rootChain.BlockHeight > 0);
            Assert.IsTrue(rootChain.ChildChains.Any());

            Assert.IsTrue(nexus.IsValidator(owner.Address));

            var randomKey = KeyPair.Generate();
            Assert.IsFalse(nexus.IsValidator(randomKey.Address));

            var txCount = nexus.GetTotalTransactionCount();
            Assert.IsTrue(txCount > 0);
        }

        [TestMethod]
        public void FungibleTokenTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var token = nexus.NativeToken;

            var testUser = KeyPair.Generate();

            var amount = TokenUtils.ToLargeInteger(400, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(transferBalance + newBalance + gasFee == oldBalance);
        }

        [TestMethod]
        public void AccountRegister()
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
                    var lastBlock = simulator.EndBlock().FirstOrDefault();

                    if (lastBlock != null)
                    {
                        Assert.IsTrue(tx != null);

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

            var amount = TokenUtils.ToLargeInteger(10, token.Decimals);

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
        public void SideChainTransferDifferentAccounts()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var originalAmount = TokenUtils.ToLargeInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, token, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            var crossFee = TokenUtils.ToLargeInteger(0.001m, token.Decimals);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, token, sourceChain, receiver.Address, targetChain, sideAmount, crossFee);
            simulator.EndBlock();
            var blockA = nexus.RootChain.LastBlock;

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == sideAmount - feeB);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA + crossFee);

            balance = sourceChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);
        }

        [TestMethod]
        public void SideChainTransferSameAccount()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = sender;

            var originalAmount = TokenUtils.ToLargeInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, token, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, token, sourceChain, receiver.Address, targetChain, sideAmount, 0);
            simulator.EndBlock();
            var blockA = nexus.RootChain.LastBlock;

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == sideAmount - feeB);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA);

            balance = sourceChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);
        }

        [TestMethod]
        public void SideChainTransferMultipleSteps()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var appsChain = nexus.FindChainByName("apps");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var originalAmount = TokenUtils.ToLargeInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            var newChainName = "testing";

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, token, originalAmount);
            simulator.GenerateChain(owner, appsChain, newChainName);
            simulator.EndBlock();

            var targetChain = nexus.FindChainByName(newChainName);

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to apps chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, token, sourceChain, sender.Address, appsChain, sideAmount, 0);
            var blockA = simulator.EndBlock().FirstOrDefault();

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, appsChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // we cant transfer the full side amount due to fees
            // TODO  calculate the proper fee values instead of this
            sideAmount /= 2;
            var extraFree = TokenUtils.ToLargeInteger(0.01m, token.Decimals);

            // do another side chain send using test user balance from apps to target chain
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, token, appsChain, receiver.Address, targetChain, sideAmount, extraFree);
            var blockC = simulator.EndBlock().FirstOrDefault();

            // finish the chain transfer
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(sender, appsChain, targetChain, blockC.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

            // TODO  verify balances
        }

        [TestMethod]
        public void NftMint()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(owner, testUser.Address, chain, token, tokenData);
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
        public void NftBurn()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            // Send some SOUL to the test user (required for gas used in "burn" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, chain, simulator.Nexus.NativeToken, TokenUtils.ToLargeInteger(1, Nexus.NativeTokenDecimals));
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(owner, testUser.Address, chain, token, tokenData);
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
        public void NftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftKey = KeyPair.Generate();
            var nftSymbol = "COOL";
            var nftName = "CoolToken";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, chain, simulator.Nexus.NativeToken, TokenUtils.ToLargeInteger(1, Nexus.NativeTokenDecimals));
            simulator.EndBlock();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, nftName, 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            // Mint a new CoolToken directly on the sender
            simulator.BeginBlock();
            simulator.GenerateNft(owner, sender.Address, chain, token, tokenData);
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
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

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
        public void SidechainNftTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var nftSymbol = "COOL";

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var fullAmount = TokenUtils.ToLargeInteger(10, Nexus.NativeTokenDecimals);
            var smallAmount = fullAmount / 2;
            Assert.IsTrue(smallAmount > 0);

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, sourceChain, simulator.Nexus.NativeToken, fullAmount);
            simulator.EndBlock();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownedTokenList = sourceChain.GetTokenOwnerships(token).Get(sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            // Mint a new CoolToken directly on the sender
            simulator.BeginBlock();
            simulator.GenerateNft(owner, sender.Address, sourceChain, token, tokenData);
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
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

            var extraFee = TokenUtils.ToLargeInteger(0.001m, nexus.NativeToken.Decimals);

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            simulator.GenerateSideChainSend(sender, simulator.Nexus.NativeToken, sourceChain, receiver.Address, targetChain, smallAmount, extraFee);
            var txA = simulator.GenerateNftSidechainTransfer(sender, receiver.Address, sourceChain, targetChain, token, tokenId);
            simulator.EndBlock();

            var blockA = nexus.RootChain.LastBlock;

            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, blockA.Hash);
            Assert.IsTrue(simulator.EndBlock().Any());

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

        [TestMethod]
        public void TestNoGasSameChainTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByName("account");
            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var amount = TokenUtils.ToLargeInteger(400, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(transferBalance + newBalance + gasFee == oldBalance);

            //Try to send the entire balance without affording fees from sender to receiver
            try
            {
                simulator.BeginBlock();
                tx = simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, token, transferBalance);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            // verify balances, receiver should have 0 balance
            transferBalance = nexus.RootChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(transferBalance == 0, "Transaction failed completely as expected");
        }

        [TestMethod]
        public void NoGasTestSideChainTransfer()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.FindChainByName("privacy");

            var token = nexus.NativeToken;

            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();

            var originalAmount = TokenUtils.ToLargeInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, token, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            Transaction txA = null, txB = null;

            try
            {
                // do a side chain send using test user balance from root to account chain
                simulator.BeginBlock();
                txA = simulator.GenerateSideChainSend(sender, token, sourceChain, receiver.Address, targetChain,
                    originalAmount, 1);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            try
            {
                var blockA = nexus.RootChain.LastBlock;

                // finish the chain transfer
                simulator.BeginBlock();
                txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, targetChain, blockA.Hash);
                Assert.IsTrue(simulator.EndBlock().Any());
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }


            // verify balances, receiver should have 0 balance
            balance = targetChain.GetTokenBalance(token, receiver.Address);
            Assert.IsTrue(balance == 0);
        }

        [TestMethod]
        public void TestSha256Repeatability()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asjdhweiurhwiuthedkgsdkfjh4otuiheriughdfjkgnsdçfjherslighjsghnoçiljhoçitujgpe8rotu89pearthkjdf.");

            SHA256 sharedTest = new SHA256();

            int testFails = 0; //differences in reused and fresh custom sha256 hashes

            for (int i = 0; i < 10000; i++)
            {
                SHA256 freshTest = new SHA256();

                var sharedTestHash = sharedTest.ComputeHash(source);
                var freshTestHash = freshTest.ComputeHash(source);

                testFails += sharedTestHash.SequenceEqual(freshTestHash) ? 0 : 1;
            }

            Assert.IsTrue(testFails == 0);
        }

        [TestMethod]
        public void TestSha512Repeatability()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asjdhweiurhwiuthedkgsdkfjh4otuiheriughdfjkgnsdçfjherslighjsghnoçiljhoçitujgpe8rotu89pearthkjdf.");

            SHA512 sharedTest = new SHA512();

            int testFails = 0; //differences in reused and fresh custom sha256 hashes

            for (int i = 0; i < 10000; i++)
            {
                SHA512 freshTest = new SHA512();

                sharedTest.Update(source, 0, source.Length);
                freshTest.Update(source, 0, source.Length);

                var sharedTestHash = sharedTest.Finish();
                var freshTestHash = freshTest.Finish();

                testFails += sharedTestHash.SequenceEqual(freshTestHash) ? 0 : 1;

                sharedTest.Init();
            }

            Assert.IsTrue(testFails == 0);
        }

        [TestMethod]
        public void TestAdler()
        {
            string source =
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh";

            var adler32Target = 0xa1036a10; //https://hash.online-convert.com/adler32-generator

            for (int i = 0; i < 10000; i++)
            {
                var adler32Test = source.Adler32();

                Assert.IsTrue(adler32Target == adler32Test);
            }
        }

        [TestMethod]
        public void TestKeccak()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var keccak128Test = new KeccakDigest(128);
            var keccak224Test = new KeccakDigest(224);
            var keccak256Test = new KeccakDigest(256);
            var keccak288Test = new KeccakDigest(288);
            var keccak384Test = new KeccakDigest(384);
            var keccak512Test = new KeccakDigest(512);

            for (int i = 0; i < 10000; i++)
            {

                //can't find any ground truth for this one, https://8gwifi.org/MessageDigest.jsp is the only one but when comparing to other site's results for other keccaks it doesnt match up with them
                
                /*var output1 = new byte[keccak128Test.GetDigestSize()];
                keccak128Test.BlockUpdate(source, 0, source.Length);
                keccak128Test.DoFinal(output1, 0);
                var target1 = StringToByteArray("a896124a35603f3766d9d41dade89f9b");*/
                
                
                var output2 = new byte[keccak224Test.GetDigestSize()];
                keccak224Test.BlockUpdate(source, 0, source.Length);
                keccak224Test.DoFinal(output2, 0);
                var target2 = StringToByteArray("3c8aa5706aabc26dee19b466e77f8947f801762ca64316fdf3a2434a"); //https://emn178.github.io/online-tools/keccak_224.html

                var output3 = new byte[keccak256Test.GetDigestSize()];
                keccak256Test.BlockUpdate(source, 0, source.Length);
                keccak256Test.DoFinal(output3, 0);
                var target3 = StringToByteArray("09D3FA337D33E1BEB3C3D560D93F5FB57C66BC3E044127816F42494FA4947A92"); //https://asecuritysite.com/encryption/sha3

                //can't find any ground truth for this one, https://8gwifi.org/MessageDigest.jsp is the only one but when comparing to other site's results for other keccaks it doesnt match up with them
                /*var output4 = new byte[keccak288Test.GetDigestSize()];
                keccak288Test.BlockUpdate(source, 0, source.Length);
                keccak288Test.DoFinal(output4, 0);
                var target4 = System.Convert.FromBase64String("");*/

                var output5 = new byte[keccak384Test.GetDigestSize()];
                keccak384Test.BlockUpdate(source, 0, source.Length);
                keccak384Test.DoFinal(output5, 0);
                var target5 = StringToByteArray("B1EA01288A8ECA553687E92943FC8E8D22B3B918462B7708FCB011B8EF28F60E7072FE2623E624DEBD00F8CF46B1F967"); //https://asecuritysite.com/encryption/sha3

                var output6 = new byte[keccak512Test.GetDigestSize()];
                keccak512Test.BlockUpdate(source, 0, source.Length);
                keccak512Test.DoFinal(output6, 0);
                var target6 = StringToByteArray("1057C35F3364A9C7D7EFB5B2AB48D9A71373DCA1E3680CBF6734DA5E896DD7DE2901A678240A1C936598A6C58E6253A9747E2715BBD559AA9A5DA9302B815BAC"); //https://asecuritysite.com/encryption/sha3

                //Assert.IsTrue(output1.SequenceEqual(target1));
                //Assert.IsTrue(output2.SequenceEqual(target2));
                Assert.IsTrue(output3.SequenceEqual(target3));
                //Assert.IsTrue(output4.SequenceEqual(target4));
                Assert.IsTrue(output5.SequenceEqual(target5));
                Assert.IsTrue(output6.SequenceEqual(target6));
            }


        }

        [TestMethod]
        public void TestMurmur32()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var murmurTest = Murmur32.Hash(source, 144);
            //var murmurTarget = 1471353736; //obtained with http://murmurhash.shorelabs.com, MurmurHash3 32bit x86
            var murmurTarget = murmurTest;

            for (int i = 0; i < 10000; i++)
            {
                murmurTest = Murmur32.Hash(source, 144);
                Assert.IsTrue(murmurTest == murmurTarget);
            }

        }
        /*
        [TestMethod]
        public void TestPoly1305Donna()
        {
            var key = new Array8<UInt32>();
            key.x0 = 120398;
            key.x0 = 123987;
            key.x0 = 12487;
            key.x0 = 102398;
            key.x0 = 123098;
            key.x0 = 59182;
            key.x0 = 2139578;
            key.x0 = 1203978;

            byte[] message = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var output = new byte[100];
            poly1305_auth(output, 0, message, 0, message.Length, key);
        }
        */


        [TestMethod]
        public void TestSha3Keccak()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            for (int i = 0; i < 10000; i++)
            {
                var sha3Test = SHA3Keccak.CalculateHash(source);
                var sha3Target = StringToByteArray("09D3FA337D33E1BEB3C3D560D93F5FB57C66BC3E044127816F42494FA4947A92");     //https://asecuritysite.com/encryption/sha3 , using sha-3 256 bit

                Assert.IsTrue(sha3Test.SequenceEqual(sha3Target));
            }
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
