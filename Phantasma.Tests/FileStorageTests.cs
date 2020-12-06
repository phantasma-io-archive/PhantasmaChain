using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.StakeContract;
using static Phantasma.Blockchain.Contracts.StorageContract;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;
using Phantasma.Blockchain.Storage;

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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 5;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = accountBalance / 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());

            //System.IO.File.WriteAllText(@"c:\code\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();
            Console.WriteLine($"{usedSpace} / {contentSize}");
            Assert.IsTrue(usedSpace == contentSize);

            Assert.IsTrue(simulator.Nexus.ArchiveExists(simulator.Nexus.RootStorage, contentMerkle.Root));
            var archive = simulator.Nexus.GetArchive(simulator.Nexus.RootStorage, contentMerkle.Root);

            //TODO not sure what that part is for...
            //for (int i=0; i<archive.BlockCount; i++)
            //{
            //    int ofs = (int)(i * archive.BlockSize);
            //    Console.WriteLine("ofs: " + ofs);
            //    var blockContent = content.Skip(ofs).Take((int)archive.BlockSize).ToArray();
            //    simulator.Nexus.WriteArchiveBlock(archive, i, blockContent);
            //}
        }

        [TestMethod]
        public void SingleUploadSuccessMaxFileSize()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

            BigInteger accountBalance = (DomainSettings.ArchiveMaxSize / 1024) / KilobytesPerStake;  //provide enough account balance for max file size available space
            accountBalance *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call
            var stakeAmount = accountBalance;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            //System.IO.File.WriteAllText(@"D:\Repos\bug_vm.txt", string.Join('\n', new VM.Disassembler(tx.Script).Instructions));
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);
        }

        //upload a file for less than available space and perform partial unstake
        [TestMethod]
        public void ReduceAvailableSpace()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake * 5;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024 / 5) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Upload a file
            var filename = "notAVirus.exe";

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

            var headerSize = CalculateRequiredSize(filename, 0);
            var contentSize = (long)(stakedAmount / MinimumValidStake * KilobytesPerStake * 1024) - (long)headerSize;
            var content = new byte[contentSize];

            var contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();
            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            var eventData = events.First(x => x.Kind == EventKind.FileCreate).Data;
            var archiveHash = new Hash(eventData.Skip(1).ToArray());

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, archiveHash).
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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            var oldSpace = contentSize;
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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace + contentSize);

            oldSpace += contentSize;
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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == oldSpace + contentSize);
        }

        //reupload a file maintaining the same name after deleting the original one
        [TestMethod]
        public void ReuploadSuccessAfterDelete()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();
            var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
            var eventData = events.First(x => x.Kind == EventKind.FileCreate).Data;
            var archiveHash = new Hash(eventData.Skip(1).ToArray());

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            var oldSpace = contentSize;

            //-----------
            //Delete the file

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "DeleteFile", testUser.Address, archiveHash).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == 0);

            //----------
            //Upload the same file: should succeed
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

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

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for userA
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, stakeAmount).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            //----------
            //Perform a valid Stake call for userB
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserB.Address, stakeAmount).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUserB.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserA.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            //----------
            //User B uploads the same file: should succeed
            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "CreateFile", testUserB.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserB.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);
        }

        //upload a duplicate of an already uploaded file but by a different owner
        [TestMethod]
        public void UploadDuplicateFileSameOwner()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUserA = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for userA
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, stakeAmount).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserA.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            //----------
            //User B uploads the same file: should succeed
            contentMerkle = new MerkleTree(content);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("storage", "CreateFile", testUserA.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUserA.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);
        }
        #endregion

        #region FailureTests

        //try unstaking below required space for currently uploaded files
        [TestMethod]
        public void UnstakeWithStoredFilesFailure()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            
            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakedAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakedAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            var oldSpace = usedSpace;

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try to unstake everything: should fail due to files still existing for this user
            var initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var stakeReduction = initialStakedAmount - MinimumValidStake;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                        .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize * 2, contentMerkle, ArchiveExtensions.Uncompressed).
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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call for minimum staking amount
            var stakeAmount = MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize);

            var oldSpace = contentSize;
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
                        .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call
            var stakeAmount = MinimumValidStake * 2;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(stakeAmount == startingSoulBalance - finalSoulBalance);

            var KilobytesPerStake = simulator.Nexus.GetGovernanceValue(simulator.Nexus.RootChain.Storage, StorageContract.KilobytesPerStakeTag);

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
                    .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, contentMerkle, ArchiveExtensions.Uncompressed).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();

            Assert.IsTrue(usedSpace == contentSize );

            var oldSpace = contentSize;

            //----------
            //Upload a file with the same name: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("storage", "CreateFile", testUser.Address, filename, contentSize, content, ArchiveExtensions.Uncompressed).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            Assert.IsTrue(usedSpace == oldSpace);
        }


        #endregion

        [TestMethod]
        public void SmallFileContractUpload()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var stakeAmount = MinimumValidStake * 5;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var sb = new System.Text.StringBuilder();
            for (int i=0; i<64; i++)
            {
                sb.AppendLine("Hello Phantasma!");
            }

            var testMsg = sb.ToString();
            var textFile = System.Text.Encoding.UTF8.GetBytes(testMsg);
            var key = ArchiveExtensions.Uncompressed;

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("storage", "WriteData", testUser.Address, "test.txt", textFile, key).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            var block = simulator.EndBlock().FirstOrDefault();

            var usedSpace = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "storage", "GetUsedSpace", testUser.Address).AsNumber();
            Assert.IsTrue(usedSpace == textFile.Length);
        }
    }
}
