using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using static Phantasma.Contracts.Native.StakeContract;
using static Phantasma.Contracts.Native.StorageContract;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class FileStorageTests
    {
        #region SuccessTests

        //stake soul and upload a file under the available space limit
        [TestMethod]
        public void SingleUploadSuccess()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 5;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = accountBalance / 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)((stakedAmount / MinimumValidStake * KilobytesPerStake) * 1024) - (long)headerSize;
            var content = new byte[contentSize];
            var rnd = new Random();
            for (int i=0; i<content.Length; i++)
            {
                content[i] = (byte)rnd.Next();
            }

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());

            //System.IO.File.WriteAllText(@"c:\code\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();
            Assert.IsTrue(usedSpace == contentSize + headerSize);

            Assert.IsTrue(simulator.Nexus.ArchiveExists(contentMerkle.Root));
            var archive = simulator.Nexus.GetArchive(contentMerkle.Root);
            for (int i=0; i<archive.BlockCount; i++)
            {
                int ofs = (int)(i * Archive.BlockSize);
                var blockContent = content.Skip(ofs).Take((int)Archive.BlockSize).ToArray();
                simulator.Nexus.WriteArchiveBlock(archive, blockContent, i);
            }
        }

        [TestMethod]
        public void SingleUploadSuccessMaxFileSize()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            BigInteger accountBalance = (DomainSettings.ArchiveMaxSize / 1024) / KilobytesPerStake;  //provide enough account balance for max file size available space
            accountBalance *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = accountBalance;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(DomainSettings.ArchiveMaxSize) - (long)headerSize;
            var content = new byte[contentSize];
            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            System.IO.File.WriteAllText(@"D:\Repos\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);
        }

        //upload a file for less than available space and perform partial unstake
        [TestMethod]
        public void ReduceAvailableSpace()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake * 5;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024 / 5) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try partial unstake: should succeed
            var initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var stakeReduction = stakedAmount / 5;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();

            Assert.IsTrue(finalStakedAmount == initialStakedAmount - stakeReduction);
        }

        //upload a file for full space, delete file and perform full unstake
        [TestMethod]
        public void UnstakeAfterUsedSpaceRelease()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == 0);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should succeed
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(finalStakedAmount == 0);

        }

        //upload more than one file for a total size that is less than the available space
        [TestMethod]
        public void CumulativeUploadSuccess()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload another file: should succeed

            filename = "giftFromTroia.exe";
            headerSize = CalculateRequiredSize(filename, 0);
            contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            content = new byte[contentSize];

            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace + contentSize + headerSize);

            oldSpace += contentSize + headerSize;
            //----------
            //Upload another file: should succeed

            filename = "JimTheEarthWORM.exe";
            headerSize = CalculateRequiredSize(filename, 0);
            contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 4) - (long)headerSize;
            content = new byte[contentSize];

            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace + contentSize + headerSize);
        }

        //reupload a file maintaining the same name after deleting the original one
        [TestMethod]
        public void ReuploadSuccessAfterDelete()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, filename).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == 0);

            //----------
            //Upload the same file: should succeed
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace);
        }

        //upload a duplicate of an already uploaded file but by a different owner
        [TestMethod]
        public void UploadDuplicateFileDifferentOwner()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for userA
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, stakeAmount).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserA.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //----------
            //Perform a valid Stake call for userB
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserB.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserB.Address, stakeAmount).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUserB.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserB.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //User A uploads a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserA.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            //----------
            //User B uploads the same file: should succeed
            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUserB.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserB.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);
        }
        #endregion

        #region FailureTests

        //try unstaking below required space for currently uploaded files
        [TestMethod]
        public void UnstakeWithStoredFilesFailure()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = usedSpace;

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should fail due to files still existing for this user
            var initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var stakeReduction = initialStakedAmount - MinimumValidStake;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace);
        }

        //try to upload a single file beyond available space
        [TestMethod]
        public void UploadBeyondAvailableSpace()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should fail due to exceeding available space
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            Assert.ThrowsException<ChainException>(() =>
            {
                var contentMerkle = new MerkleTree(content);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize * 2, contentMerkle, ArchiveFlags.None, new byte[0]).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            })
            ;
            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == 0);
        }

        //try to upload multiple files that individually dont go above available space, but that cumulatively do so
        [TestMethod]
        public void CumulativeUploadMoreThanAvailable()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;
            //----------
            //Upload a file: should fail due to exceeding available storage capacity

            filename = "giftFromTroia.exe";
            headerSize = CalculateRequiredSize(filename, 0);
            contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            content = new byte[contentSize];

            Assert.ThrowsException<ChainException>(() =>
            {
                contentMerkle = new MerkleTree(content);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            Assert.IsTrue(usedSpace == oldSpace);
        }

        //upload a file with the same name as an already uploaded file
        [TestMethod]
        public void UploadDuplicateFilename()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //-----------
            //Upload a file: should succeed
            var filename = "notAVirus.exe";
            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakeAmount / MinimumValidStake * KilobytesPerStake * 1024 / 2) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveFlags.None, new byte[0]).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize + headerSize);

            var oldSpace = contentSize + headerSize;

            //----------
            //Upload a file with the same name: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "UploadFile", testUser.Address, filename, contentSize, content, ArchiveFlags.None, new byte[0]).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            Assert.IsTrue(usedSpace == oldSpace);
        }


        #endregion


    }
}
