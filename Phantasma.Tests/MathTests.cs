using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using Phantasma.Numerics;
using Phantasma.Cryptography;

namespace Phantasma.Tests
{
    [TestClass]
    public class MathTests
    {
        #region BASE CONVERSIONS
        [TestMethod]
        public void Base16Tests()
        {
            var bytes = new byte[Address.LengthInBytes];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var base16 = Base16.Encode(bytes);

            Assert.IsTrue(base16.Length == bytes.Length * 2);

            var output = Base16.Decode(base16);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }

        [TestMethod]
        public void Base58Tests()
        {
            var bytes = new byte[Address.LengthInBytes];
            var rnd = new Random(39545);
            rnd.NextBytes(bytes);

            var base58 = Base58.Encode(bytes);
            var output = Base58.Decode(base58);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));

            bytes = new byte[Address.LengthInBytes];
            bytes[0] = 1;
            base58 = Base58.Encode(bytes);
            output = Base58.Decode(base58);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }

        [TestMethod]
        public void Test35ByteEncodeDecode58()
        {
            var input = new byte[35];

            var temp = Base58.Encode(input);
            var output = Base58.Decode(temp);

            Assert.IsTrue(output.Length == input.Length);
        }
        #endregion

        #region BIG INT

        [TestMethod]
        public void BigIntZeroComparison()
        {
            BigInteger a = 0;
            Assert.IsTrue((a != 0) == false);
            Assert.IsTrue(a == 0);
            Assert.IsTrue((a > 0) == false);
            Assert.IsTrue((a < 0) == false);
        }


        struct BigIntStruct
        {
            public BigInteger a;
        }
        [TestMethod]
        public void BigIntStructComparisonExplicitInit()
        {
            BigIntStruct s = new BigIntStruct() {a = 0};

            Assert.IsTrue((s.a != 0) == false);
            Assert.IsTrue(s.a == 0);
            Assert.IsTrue((s.a > 0) == false);
            Assert.IsTrue((s.a < 0) == false);
        }

        [TestMethod]
        public void BigIntStructComparisonImplicitInit()
        {
            BigIntStruct s = new BigIntStruct();

            Assert.IsTrue((s.a != 0) == false);
            Assert.IsTrue(s.a == 0);
            Assert.IsTrue((s.a > 0) == false);
            Assert.IsTrue((s.a < 0) == false);
        }

        //[TestMethod]
        //public void BigIntAdd()
        //{
        //    string x = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string y = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856109821340985938475923184039457898237578934523098402583058012398423495345634569283473901830459823487189237468749574890570319478923856834756391847239011239087345972173402384230957498573190840";

        //    string z = "453870adc8b058c47b58be10c4676c163567c96cb4b3ab4c7a4833b9b3b4d57c418f1fc891830a76f948acc71468bc22ada829c7ab41c08dc8accc7c80b26a4efc9dbb96a92b7646530b5b7b787b02bbcc9acbb8ccad68aeb59750ac5b0678ed03a91f89257dd70e6ca0cfb187979a7821578ebf68febabaed04698646c98ba678a9b9e6504cbccf0780ca88098b4069";

        //    var a = new BigInteger(x, 16);
        //    var b = new BigInteger(y, 16);

        //    var target = new BigInteger(z, 16);

        //    var result = a + b;

        //    Assert.IsTrue(result == target);
        //}

        //[TestMethod]
        //public void BigIntAddNegatives()
        //{
        //    string x = "1000";
        //    string y = "2000";

        //    Assert.IsTrue((new BigInteger("-" + x, 10) + new BigInteger(y, 10)).ToDecimal() == "1000");
        //    Assert.IsTrue((new BigInteger(x, 10) + new BigInteger("-" + y, 10)).ToDecimal() == "-1000");
        //    Assert.IsTrue((new BigInteger("-" + x, 10) + new BigInteger("-" + y, 10)).ToDecimal() == "-3000");

        //    Assert.IsTrue((new BigInteger("-" + y, 10) + new BigInteger(x, 10)).ToDecimal() == "-1000");
        //    Assert.IsTrue((new BigInteger(y, 10) + new BigInteger("-" + x, 10)).ToDecimal() == "1000");
        //    Assert.IsTrue((new BigInteger("-" + y, 10) + new BigInteger("-" + x, 10)).ToDecimal() == "-3000");
        //}

        //[TestMethod]
        //public void BigIntSub()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856109821340985938475923184039457898237578934523098402583058012398423495345634569283473901830459823487189237468749574890570319478923856834756391847239011239087345972173402384230957498573190840";
        //    string bz = "2126008c9dae179deb05b32ba1f7033430f6a945b1aac6dbec162f50b2ae5366c397c24a6f2ffc13c8f7c5c3eff7d20fa2e616c384c08fdb581e1a365031f7c40b6d74abb804ec00400356cb17cb00749c16628e640100019090c23a58007039ff401158010f49ffd9f23e90d99167e90f1083ef0013f397e41ff78422577abfed7b76ffd005b488f4d2377d23592fe9";

        //    var a = new BigInteger(bx, 16);
        //    var b = new BigInteger(by, 16);

        //    var c = a - b;
        //    var target = new BigInteger(bz, 16);

        //    Assert.IsTrue(c == target);
        //}

        //[TestMethod]
        //public void BigIntSubNegatives()
        //{
        //    int x = 1000;
        //    int y = 2000;

        //    Assert.IsTrue((new BigInteger(-x) - new BigInteger(y)).ToDecimal() == "-3000");
        //    Assert.IsTrue((new BigInteger(x) - new BigInteger(-y)).ToDecimal() == "3000");
        //    Assert.IsTrue((new BigInteger(-x) - new BigInteger(-y)).ToDecimal() == "1000");

        //    Assert.IsTrue((new BigInteger(-y) - new BigInteger(x)).ToDecimal() == "-3000");
        //    Assert.IsTrue((new BigInteger(y) - new BigInteger(-x)).ToDecimal() == "3000");
        //    Assert.IsTrue((new BigInteger(-y) - new BigInteger(-x)).ToDecimal() == "-1000");
        //}

        //[TestMethod]
        //public void BigIntMult()
        //{
        //    string x = "120938109581209348298572913834710238901381847238471902348083410abefbaebf11298731982873819238750985610982134098593847592318403945789823757893452309840258305801239842349534563456928347390183045982348718923746874957489057031947892385683475639184723901";
        //    string y = "671035689173764871689727651290298572184350928350918429057384756781347915789013472348957aebfdc120398273958346578694871394872059834956475623918473095789045789134723845634758931470275348956428973019478927";
        //    string z = "742d9e573c9dd18b1ff173c47c69b9d6eb11462370e26720d642f3a430268ddd8e1996c9ccba94a880345a7d623ba11ac11bf3022b72a26ab3761a453659f22f882c71901da998617b99df26253dda2990985390b6066b9e8dcfb952224f74257031c43f4859c3ff2d9f4eb63f0da95ac5afdf6583822bea325740caf325148848f6ccbf81d54bc2960234ee35c4b0e45d108c8f39e9e6a03303b43aa0130eb70dd231da92fb6e588e648f0ee204385a506b3fa4922cd6f77b0b2940f5e4c9acd0b83cbc7449e06bf1a7688be21642f4120a67fc66e1605524a8357b362f3827";

        //    var a = new BigInteger(x, 16);
        //    var b = new BigInteger(y, 16);
        //    var target = new BigInteger(z, 16);

        //    var c = a * b;
        //    var tmp = (new BigInteger(x, 16) * new BigInteger(y, 16)).ToString();
        //    var tmp2 = target.ToDecimal();
        //    Assert.IsTrue(c == target);
        //}

        //[TestMethod]
        //public void BigIntMultNegatives()
        //{
        //    int x = 100000;
        //    int y = 1000;

        //    Assert.IsTrue((new BigInteger(-x) * new BigInteger(y)).ToDecimal() == "-100000000");
        //    Assert.IsTrue((new BigInteger(x) * new BigInteger(-y)).ToDecimal() == "-100000000");
        //    Assert.IsTrue((new BigInteger(-x) * new BigInteger(-y)).ToDecimal() == "100000000");

        //    Assert.IsTrue((new BigInteger(-y) * new BigInteger(x)).ToDecimal() == "-100000000");
        //    Assert.IsTrue((new BigInteger(y) * new BigInteger(-x)).ToDecimal() == "-100000000");
        //    Assert.IsTrue((new BigInteger(-y) * new BigInteger(-x)).ToDecimal() == "100000000");
        //}

        //[TestMethod]
        //public void BigIntMultiDigitDiv()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "18273910598278301928412039581203918927840501928391029237623187492834729018034982903478091248398457";
        //    string bq = "21e81159046f0d4c1fc54daf52b4638c36ec204b0642bed62e832bc7bdd850b615d279010d872dfdc1e1282595eae06601de4c5ed10ff10c3d6e487736860297daef70d22f521dd8767b8a41415879a18435266b07a798c231224d2e444faa6";
        //    string br = "915d55661358efddbdfa4419bfed43863858cac650ed298422ae17164681ba4a66869b950689671178560901cf2cd71bf";

        //    var numerator = new BigInteger(bx, 16);
        //    var denominator = new BigInteger(by, 16);

        //    var target_quot = new BigInteger(bq, 16);
        //    var target_rem = new BigInteger(br, 16);

        //    BigInteger quot;
        //    BigInteger rem;
        //    BigInteger.DivideAndModulus(numerator, denominator, out quot, out rem);

        //    Assert.IsTrue(quot == target_quot);
        //    Assert.IsTrue(rem == target_rem);
        //}

        //[TestMethod]
        //public void BigIntSingleDigitDiv()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "A";
        //    string bq = "51e52761eb7ec04eb84b8dc9eb7ebf6eb84b8ef51eb1f4ed1eb1e8d51eb5ba4f37524e759a28d2089b66c208d04d3e8ea6d833a28ccea6ba80a25228a71d1b426cd5c0351a8d1b6ba8d88e9ed9d19c26ba275838f3beba269e8675855c05875268ba8d80eba41a71d20f3e9b80ed9b80f3867558540ef1dbda8380d520e73851eb50f3eb4d0ec1133042680423e9f37";
        //    string br = "3";

        //    var numerator = new BigInteger(bx, 16);
        //    var denominator = new BigInteger(by, 16);

        //    var target_quot = new BigInteger(bq, 16);
        //    var target_rem = new BigInteger(br, 16);

        //    BigInteger quot;
        //    BigInteger rem;
        //    BigInteger.DivideAndModulus(numerator, denominator, out quot, out rem);

        //    Assert.IsTrue(quot == target_quot);
        //    Assert.IsTrue(rem == target_rem);
        //}

        //[TestMethod]
        //public void BigIntDivNegatives()
        //{
        //    int x = 100000;
        //    int y = 1000;

        //    Assert.IsTrue((new BigInteger(-x) / new BigInteger(y)).ToDecimal() == "-100");
        //    Assert.IsTrue((new BigInteger(x) / new BigInteger(-y)).ToDecimal() == "-100");
        //    Assert.IsTrue((new BigInteger(-x) / new BigInteger(-y)).ToDecimal() == "100");

        //    Assert.IsTrue((new BigInteger(-y) / new BigInteger(x)).ToDecimal() == "0");
        //    Assert.IsTrue((new BigInteger(y) / new BigInteger(-x)).ToDecimal() == "0");
        //    Assert.IsTrue((new BigInteger(-y) / new BigInteger(-x)).ToDecimal() == "0");
        //}

        //[TestMethod]
        //public void TestSubtractionBorrowing()
        //{
        //    var x1 = new BigInteger(new uint[] { 0x01000001 });
        //    var y1 = new BigInteger(new uint[] { 0xfefeff });


        //    var groundTruth1 = new BigInteger(new uint[] { 0x010102 });

        //    var z1 = x1 - y1;

        //    Assert.IsTrue(z1 == groundTruth1);
        //}

        //[TestMethod]
        //public void TestToString()
        //{
        //    var x = new BigInteger("24750ed6468b0f8c43d270e609e1224613046930e24730e4730ae32506e0327000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 16);
        //    Assert.IsTrue(x.ToDecimal() == "343255395128441705738430800749513158191704924924659029403775342881472703497041611993270924407633010896488451591654215164571079711934795651275975982617029318658658666648753731952553042523384561627517385625811279929494676406022746972049340844301447752320787688611375601417145691357093739519857589421036607980254378910944708786049273704140249078873935879289476950237030005460408499081908841901857811534210748336837493224463100031667809871040667649289996624956751872");
        //}

        //[TestMethod]
        //public void TestComparison()
        //{
        //    var x = new BigInteger("1000000", 16);
        //    var y = new BigInteger("ffffff", 16);

        //    var z = x / y;

        //    Assert.IsTrue(z.ToDecimal() == "1");

        //    var test1 = new BigInteger("0100", 16);

        //    Assert.IsTrue(test1 > z);
        //}

        //[TestMethod]
        //public void TestLeftShift()
        //{
        //    var x = new BigInteger("123a876b234587c621e9387304f0912309823498712398723985719283701938", 16);

        //    //testing extra digit
        //    var target = new BigInteger("123a876b234587c621e9387304f09123098234987123987239857192837019380000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 16);
        //    var y = x << 1000;  
        //    Assert.IsTrue(y == target);

        //    //testing no extra digit
        //    target = new BigInteger("24750ed6468b0f8c43d270e609e1224613046930e24730e4730ae32506e0327000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", 16);
        //    y = x << 1281;  
        //    Assert.IsTrue(y == target);
        //}

        //[TestMethod]
        //public void TestRightShift()
        //{
        //    var x = new BigInteger("111230981409285093459012840956983409578093148394857023194801239875784567834568931749081123437456abecdf", 16);
        //    
        //    //to test extra shrinkage off
        //    var target = new BigInteger("11123098140928509345901284095698340957809314839485702", 16);
        //    var y = x >> 196;
        //    Assert.IsTrue(y == target);
        //    
        //    //to test extra shrinkage on
        //    target = new BigInteger("111230981409285093459012840956983409578093148394", 16);
        //    y = x >> 216;   
        //    Assert.IsTrue(y == target);
        //}

        //[TestMethod]
        //public void TestXor()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856109821340985938475923184039457898237578934523098402583058012398423495345634569283473901830459823487189237468749574890570319478923856834756391847239011239087345972173402384230957498573190840";

        //    string bz = "2126008da6ae18a27b06bdeca21703d43117a94ab2ab4b2c743633b1b3b2d57b3c68dfb691700474f9084ac410083210ad2629c78b41b06da8222a7a7032084cfc9dbb54480b740040035b7b787b00bbac1aa3b6ac01000eb1974e4a580070ca03401f680171d6006a1ecfb16797987711178c3f00fc34b8ece0098426a98b407685b900500abc88f77ec887e56b3069";

        //    BigInteger x = new BigInteger(bx, 16);
        //    BigInteger y = new BigInteger(by, 16);

        //    var result = x ^ y;

        //    BigInteger target = new BigInteger(bz, 16);

        //    Assert.IsTrue(result == target);
        //}

        //[TestMethod]
        //public void TestBitwiseAnd()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856109821340985938475923184039457898237578934523098402583058012398423495345634569283473901830459823487189237468749574890570319478923856834756391847239011239087345972173402384230957498573190840";

        //    string bz = "120938101101201100290012112834210228101101043010030900040001000082932009000983010020310182304509004100001000081010455101084031010000002130900123098400000000010010401401105634500200013101830411803480109206008701410000100001008820014034014301001230011010003301120073002100230801010012100800";

        //    BigInteger x = new BigInteger(bx, 16);
        //    BigInteger y = new BigInteger(by, 16);

        //    var result = x & y;

        //    BigInteger target = new BigInteger(bz, 16);

        //    Assert.IsTrue(result == target);
        //}

        //[TestMethod]
        //public void TestBitwiseOr()
        //{
        //    string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829";
        //    string by = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856109821340985938475923184039457898237578934523098402583058012398423495345634569283473901830459823487189237468749574890570319478923856834756391847239011239087345972173402384230957498573190840";

        //    string bz = "332f389db7af38b37b2fbdfeb33f37f5333fb95bb3af7b3c773f33b5b3b3d57bbefbffbf91798775f9287bc592387719ad6729c79b41b87db8677b7b7872394dfc9dbb75789b752349875b7b787b01bbbc5ab7b7bc57345eb3974f7b598374db83749f789377d6876b5fcfb17797997799378d7f34fd77b9ecf2398536b98b737797b973502bbcabff7fc987f77b3869";

        //    BigInteger x = new BigInteger(bx, 16);
        //    BigInteger y = new BigInteger(by, 16);

        //    var result = x | y;

        //    BigInteger target = new BigInteger(bz, 16);

        //    Assert.IsTrue(result == target);
        //}

        //[TestMethod]
        //public void TestZeroSign()
        //{
        //    //TODO: try all possible operations that could change a large int to 0, and validate if the sign changes accordingly
        //}

        //[TestMethod]
        //public void TestModPow()
        //{
        //    BigInteger b = new BigInteger("332f389d332f3831332f389e332f37a5332f3959332f3914332f31853331947182937109805983456120394582304719284720459801283490657359687231098405982130983123498759234823019834589723985734582314097359837493817498709346908723498721309481309834095734895729689230853490833333129873102938abfe29810296723829", 16);
        //    BigInteger exp = new BigInteger(100);
        //    BigInteger mod = new BigInteger("120398120948109480194811238927958714290501938412092389023484903", 16);

        //    var result = BigInteger.ModPow(b, exp, mod);

        //    var target = new BigInteger("24c59f542554271f199bb50074796df8a4a0a8bafa6d3bc25f8f96ea1fce4", 16);

        //    Assert.IsTrue(result == target);

        //    //TODO: we cant test negative exponentials yet because we need to implement the modInverse operation first!
        //    
        //    //result = b.ModPow(-256, mod);
        //    //target = new BigInteger("");
        //    //Assert.IsTrue(result == target);
        //    
        //}

        //[TestMethod]
        //public void TestBitLength()
        //{
        //    var n = new BigInteger("0", 16);
        //    Assert.IsTrue(n.GetBitLength() == 0);

        //    n++;

        //    for (int i = 2; i <= 2048; i++)
        //    {
        //        n <<= 1;
        //        Assert.IsTrue(n.GetBitLength() == i);
        //    }
        //}

        //[TestMethod]
        //public void TestTwosComplement()
        //{
        //    var posNum =
        //        "12039895083450981409812309823049859076560924380198409183409284534987689509850239801948203948935795867389523019480192384290859346789038012983402587938457";

        //    var posBigint = new BigInteger(posNum, 16);

        //    var negArray = (-posBigint).ToSignedByteArray();

        //    var negBigInt = BigInteger.FromSignedArray(negArray);

        //    Assert.IsTrue(posBigint.Sign== 1);
        //    Assert.IsTrue(negBigInt.Sign== -1);
        //    Assert.IsTrue(negBigInt.ToUintArray().SequenceEqual(posBigint.ToUintArray()));
        //}
        //#endregion

        //#region Proof of Work
        //[TestMethod]
        //public void PowDifficulty()
        //{
        //    int diff;
        //    var bytes = new byte[32];

        //    bytes[0] = 1;
        //    diff = new Hash(bytes).GetDifficulty();
        //    Assert.IsTrue(diff == 255);

        //    bytes[31] = 1;
        //    diff = new Hash(bytes).GetDifficulty();
        //    Assert.IsTrue(diff == 7);

        //    bytes[31] = 128;
        //    diff = new Hash(bytes).GetDifficulty();
        //    Assert.IsTrue(diff == 0);
        //}
        #endregion
    }
}
