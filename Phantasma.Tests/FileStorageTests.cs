using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.Native.EnergyContract;
using static Phantasma.Blockchain.Contracts.Native.StorageContract;

namespace Phantasma.Tests
{
    [TestClass]
    public class FileStorageTests
    {
        [TestMethod]
        public void UnstakeWithStoredFilesFailure()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long) (BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];
            
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should fail due to files still existing for this user
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = initialStakedAmount - BaseEnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);


        }

        [TestMethod]
        public void UnstakeWithStoredFilesSuccess()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should succeed
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = initialStakedAmount - BaseEnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(finalStakedAmount == 0);

        }

        [TestMethod]
        public void UploadMoreThanAvailable()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should fail due to exceeding available space
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize * 2, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
            ;
            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);
        }

        [TestMethod]
        public void CumulativeUploadMoreThanAvailable()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload a file: should fail due to exceeding available storage capacity

            filename = "giftFromTroia.exe";
            headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            contentSize = (long)(BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                            .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                })
                ;

            Assert.IsTrue(usedSpace == oldSpace);
        }

        [TestMethod]
        public void SingleUploadSuccess()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(BaseEnergyRatioDivisor * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize * 2, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
            ;
            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);
        }

        [TestMethod]
        public void CumulativeUploadSuccess()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload another file: should succeed

            filename = "giftFromTroia.exe";
            headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
                ;

            Assert.IsTrue(usedSpace == oldSpace);
        }

        [TestMethod]
        public void ReuploadSuccessAfterDelete()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = BaseEnergyRatioDivisor * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "CalculateRequiredSize", filename, 0);
            var contentSize = (long)(stakeAmount * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = (BigInteger)simulator.Nexus.RootChain.InvokeContract("storage", "GetUsedSpace", testUser.Address);

            Assert.IsTrue(usedSpace == 0);

            //----------
            //Upload another file: should succeed

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
                ;

            Assert.IsTrue(usedSpace == oldSpace);
        }
    }
}
