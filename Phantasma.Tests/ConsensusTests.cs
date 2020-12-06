using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.Storage;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.ConsensusContract;
using static Phantasma.Blockchain.Contracts.ValidatorContract;
using static Phantasma.Domain.ValidatorType;
using static Phantasma.Domain.DomainSettings;
using static Phantasma.Blockchain.Nexus;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class ConsensusTests
    {
        static PhantasmaKeys owner = PhantasmaKeys.Generate();
        static Nexus nexus = new Nexus("simnet", null, null);
        static NexusSimulator simulator;


        private static PhantasmaKeys[] validatorKeyPairs =
            {owner, PhantasmaKeys.Generate(), PhantasmaKeys.Generate(), PhantasmaKeys.Generate(), PhantasmaKeys.Generate()};

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
                    CallContract(ValidatorContractName, nameof(ValidatorContract.SetValidator), secondValidator.Address, 1, Primary).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock().First();

            for (int i = 2; i < validatorKeyPairs.Length; i++)
            {
                var newValidator = validatorKeyPairs[i];
                AddNewValidator(newValidator);

                var validatorType = simulator.Nexus.RootChain
                    .InvokeContract(simulator.Nexus.RootStorage, ValidatorContractName, nameof(ValidatorContract.GetValidatorType),
                        newValidator.Address).AsEnum<ValidatorType>();

                Assert.IsTrue(validatorType != Invalid);
            }

            
        }

        private void AddNewValidator(PhantasmaKeys newValidator)
        {
            Timestamp startTime = simulator.CurrentTime;
            Timestamp endTime = simulator.CurrentTime.AddDays(1);
            var pollName = SystemPoll + ValidatorPollTag;

            var validators = (ValidatorEntry[]) simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.ValidatorContractName,
                nameof(ValidatorContract.GetValidators)).ToObject(typeof(ValidatorEntry[]));

            var activeValidators = validators.Where(x => x.address != Address.Null);
            var activeValidatorCount = activeValidators.Count();

            var choices = new PollChoice[activeValidatorCount + 1];

            for (int i = 0; i < activeValidatorCount; i++)
            {
                choices[i] = new PollChoice() {value = validators[i].address.ToByteArray() };
            }

            choices[activeValidatorCount] = new PollChoice() {value = newValidator.Address.ToByteArray() };

            var serializedChoices = choices.Serialize();

            //start vote for new validator
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(ConsensusContractName, nameof(ConsensusContract.InitPoll),
                        owner.Address, pollName, DomainSettings.ValidatorsOrganizationName, ConsensusMode.Majority, startTime, endTime, serializedChoices, 1).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();

            for(int i = 0; i < activeValidatorCount; i++)
            {
                var validator = validatorKeyPairs[i];

                //have each already existing validator vote on themselves to preserve the validator order
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(validator, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                        AllowGas(validator.Address, Address.Null, 1, 9999).
                        CallContract(ConsensusContractName, nameof(ConsensusContract.SingleVote),
                            validator.Address, pollName, i).
                        SpendGas(validator.Address).
                        EndScript());
                simulator.EndBlock();
            }

            //skip until the voting is over
            simulator.TimeSkipDays(1.5);

            var votingRank = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, ConsensusContractName, nameof(ConsensusContract.GetRank), pollName,
                choices[activeValidatorCount].Serialize()).AsNumber();

            //call SetValidator for each set validator address
            for (int i = 0; i <= activeValidatorCount; i++)
            {
                var validatorChoice = choices[i].value;

                ValidatorType validatorType = i < 2 ? Primary : Secondary;

                votingRank = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, ConsensusContractName, nameof(ConsensusContract.GetRank), pollName, validatorChoice).AsNumber();

                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().
                        AllowGas(owner.Address, Address.Null, 1, 9999).
                        CallContract(ValidatorContractName, nameof(ValidatorContract.SetValidator), validatorChoice, votingRank, validatorType).
                        SpendGas(owner.Address).
                        EndScript());
                simulator.EndBlock().First();
            }

        }

        [TestMethod]
        [Ignore] //TODO
        public void TestVote()
        {
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulator = new NexusSimulator(nexus, owner, 1234);
            CreateValidators();
        }
    }
}
