using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.Native.ConsensusContract;
using static Phantasma.Blockchain.Contracts.Native.ValidatorContract;
using static Phantasma.Blockchain.Nexus;

namespace Phantasma.Tests
{
    [TestClass]
    public class ConsensusTests
    {
        static KeyPair owner = KeyPair.Generate();
        static NexusSimulator simulator = new NexusSimulator(owner, 1234);
        static Nexus nexus = simulator.Nexus;

        private static KeyPair[] validatorKeyPairs =
            {owner, KeyPair.Generate(), KeyPair.Generate(), KeyPair.Generate(), KeyPair.Generate()};

        private void CreateValidators()
        {
            simulator.blockTimeSkip = TimeSpan.FromSeconds(10);
            
            var fuelAmount = UnitConversion.ToBigInteger(10, FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, StakingTokenDecimals);

            for(int i = 1; i < validatorKeyPairs.Length; i++)
            {
                var validator = validatorKeyPairs[i];
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, validator.Address, nexus.RootChain, FuelTokenSymbol, fuelAmount);
                simulator.GenerateTransfer(owner, validator.Address, nexus.RootChain, StakingTokenSymbol, stakeAmount);
                simulator.EndBlock();
            }

            // make first validator allocate 5 validator spots       
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(GovernanceContractName, nameof(GovernanceContract.SetValue), ValidatorCountTag, new BigInteger(5)).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();

            // make validator candidates stake enough for stake master status
            for (int i = 1; i < validatorKeyPairs.Length; i++)
            {
                var validator = validatorKeyPairs[i];

                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(validator, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                        AllowGas(validator.Address, Address.Null, 1, 9999).
                        CallContract(StakeContractName, nameof(StakeContract.Stake), validator.Address, stakeAmount).
                        SpendGas(validator.Address).
                        EndScript());
                simulator.EndBlock();
            }

            var secondValidator = validatorKeyPairs[1];

            // set a second validator, no election required because theres only one validator for now
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(ValidatorContractName, nameof(ValidatorContract.SetValidator), secondValidator.Address, 1, ValidatorType.Primary).
                    SpendGas(owner.Address).
                    EndScript());
            var block = simulator.EndBlock().First();

            for (int i = 2; i < validatorKeyPairs.Length; i++)
            {
                var newValidator = validatorKeyPairs[i];
                AddNewValidator(newValidator);
            }

            
        }

        private void AddNewValidator(KeyPair newValidator)
        {
            var startTime = simulator.CurrentTime;
            var endTime = simulator.CurrentTime.AddDays(1);
            var pollName = SystemPoll + ValidatorPollTag;

            var validators = (ValidatorEntry[]) simulator.Nexus.RootChain.InvokeContract(Nexus.ValidatorContractName,
                nameof(ValidatorContract.GetValidators)).ToObject(typeof(ValidatorEntry[]));

            var activeValidators = validators.Where(x => x.address != Address.Null);
            var activeValidatorCount = activeValidators.Count();

            var choices = new PollChoice[activeValidatorCount + 1];

            for (int i = 0; i < activeValidatorCount; i++)
            {
                choices[i] = new PollChoice() {value = validators[i].address.PublicKey};
            }

            choices[activeValidatorCount] = new PollChoice() {value = newValidator.Address.PublicKey};

            //start vote for new validator
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(ConsensusContractName, nameof(ConsensusContract.InitPoll),
                        owner.Address, pollName, ConsensusKind.Validators, ConsensusMode.Majority, startTime, endTime, choices, 1).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();

            for(int i = 0; i < activeValidatorCount; i++)
            {
                var validator = validatorKeyPairs[i];

                //have each already existing validator vote on themselves to preserve the validator order
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                        AllowGas(owner.Address, Address.Null, 1, 9999).
                        CallContract(ConsensusContractName, nameof(ConsensusContract.InitPoll),
                            owner.Address, pollName, ConsensusKind.Validators, ConsensusMode.Majority, startTime, endTime, choices, 1).
                        SpendGas(owner.Address).
                        EndScript());
                simulator.EndBlock();
            }

            
        }

        [TestMethod]
        public void TestVote()
        {
            CreateValidators();
        }
    }
}
