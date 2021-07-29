using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace CrossChainDemo
{
    public class Demo
    {
        private static string api = ""; //rpc节点url
        private static byte[] prikey = new byte[0]; //迁移账户私钥
        private static readonly UInt256 gas_hash = UInt256.Parse(""); //Neo2  gas id
        private static readonly UInt160 proxyHash = UInt160.Parse("");//跨链代理合约Hash
        private static readonly UInt160 nNeoHash = UInt160.Parse(""); //nNeo合约Hash
        private static readonly BigInteger N3Id = BigInteger.Parse("11"); //N3跨链Id
        private static readonly byte[] N3ReceiveAddress = new byte[] { }; //N3收款地址
        private static readonly BigInteger migrationAmount = BigInteger.Parse("100"); //资产迁移数量
        private static readonly BigInteger projectIndex = BigInteger.Parse("10086");//跨链项目index


        public static void Main() 
        {
            KeyPair keyPair = new KeyPair(prikey);
            var addr = Contract.CreateSignatureContract(keyPair.PublicKey);

            //构建交易
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(
                    proxyHash,//代理合约
                    "lock",//合约方法名
                    nNeoHash,//需要迁移的资产Hash
                    Contract.CreateSignatureContract(keyPair.PublicKey).ScriptHash, //迁移的资产地址小端序
                    new ContractParameter() { Type = ContractParameterType.Integer, Value = N3Id }, // N3 链Id
                    N3ReceiveAddress, // N3收款地址小端序
                    new ContractParameter() { Type = ContractParameterType.Integer, Value = migrationAmount } //资产转移数量
                    new ContractParameter() { Type = ContractParameterType.Integer, Value = projectIndex }//跨链项目index
                    );
                script = sb.ToArray();
            }

            //拼交易
            InvocationTransaction tx = MakeTran(null, addr.Address, 0, script);

            tx.Attributes = GetAttribute(addr.ScriptHash);

            var signature = tx.Sign(keyPair);

            tx.Witnesses = GetWitness(signature, keyPair.PublicKey);

            Console.WriteLine("txid: " + tx.Hash.ToString());

            byte[] data = tx.ToArray();
            string rawdata = data.ToHexString();
        }

        public static InvocationTransaction MakeTran(string targetAddr, string myAddr, decimal amount, byte[] script)
        {
            List<Utxo> gasList = GetGasBalanceByAddress(myAddr);

            byte[] num = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(num);
            }

            var tx = new InvocationTransaction();
            tx.Attributes = new TransactionAttribute[] { };
            tx.Version = 1; //若花费 sys_fee, version 就是 1
            tx.Inputs = new CoinReference[] { };
            tx.Outputs = new TransactionOutput[] { };
            tx.Witnesses = new Witness[] { };
            tx.Script = script;

            //计算系统费
            string result = InvokeRpc("invokescript", script?.ToHexString());
            Console.WriteLine("invoke result:" + result);
            var consume = JObject.Parse(result)["result"]["gas_consumed"].ToString();
            decimal sys_fee = decimal.Parse(consume) - 10;

            //计算网络费
            decimal fee = 0;
            if (tx.Size > 1024)
            {
                fee += 0.001m;
                fee += tx.Size * 0.00001m;
            }

            //总费用
            decimal gas_consumed = sys_fee + fee + amount;

            gasList.Sort((a, b) =>
            {
                if (a.value > b.value)
                    return 1;
                else if (a.value < b.value)
                    return -1;
                else
                    return 0;
            });

            tx.Gas = Fixed8.FromDecimal(sys_fee);

            decimal count = decimal.Zero;

            //构造 UTXO 的 vin 和 vout
            List<CoinReference> coinList = new List<CoinReference>();
            for (int i = 0; i < gasList.Count; i++)
            {
                CoinReference coin = new CoinReference();
                coin.PrevHash = gasList[i].txid;
                coin.PrevIndex = (ushort)gasList[i].n;
                coinList.Add(coin);
                count += gasList[i].value;
                if (count >= gas_consumed)
                    break;
            }

            tx.Inputs = coinList.ToArray();

            if (count >= gas_consumed)
            {
                List<TransactionOutput> list_outputs = new List<TransactionOutput>();
                if (gas_consumed > decimal.Zero && targetAddr != null)
                {
                    TransactionOutput output = new TransactionOutput();
                    output.AssetId = gas_hash;
                    output.Value = Fixed8.FromDecimal(gas_consumed);
                    output.ScriptHash = targetAddr.ToScriptHash();
                    list_outputs.Add(output);
                }

                //找零
                var change = count - gas_consumed;
                if (change > decimal.Zero)
                {
                    TransactionOutput outputchange = new TransactionOutput();
                    outputchange.AssetId = gas_hash;
                    outputchange.ScriptHash = myAddr.ToScriptHash();
                    outputchange.Value = Fixed8.FromDecimal(change);
                    list_outputs.Add(outputchange);
                }
                tx.Outputs = list_outputs.ToArray();
            }
            else
            {
                throw new Exception("no enough money!");
            }

            return tx;
        }

        public static string InvokeRpc(string method, string data)
        {
            string input = @"{
	            'jsonrpc': '2.0',
                'method': '&',
	            'params': ['#'],
	            'id': '1'
                }";

            input = input.Replace("&", method);
            input = input.Replace("#", data);

            string result = HttpPost(api, input);
            return result;
        }

        public static string HttpGet(string url)
        {
            WebClient wc = new WebClient();
            return wc.DownloadString(url);
        }

        public static string HttpPost(string url, string data)
        {
            HttpWebRequest req = WebRequest.CreateHttp(new Uri(url));
            req.ContentType = "application/json;charset=utf-8";

            req.Method = "POST";
            //req.Accept = "text/xml,text/javascript";
            req.ContinueTimeout = 10000;

            byte[] postData = Encoding.UTF8.GetBytes(data);
            Stream reqStream = req.GetRequestStream();
            reqStream.Write(postData, 0, postData.Length);
            //reqStream.Dispose();

            HttpWebResponse rsp = (HttpWebResponse)req.GetResponse();
            string result = GetResponseAsString(rsp);

            return result;
        }

        private static string GetResponseAsString(HttpWebResponse rsp)
        {
            Stream stream = null;
            StreamReader reader = null;

            try
            {
                // 以字符流的方式读取HTTP响应
                stream = rsp.GetResponseStream();
                reader = new StreamReader(stream, Encoding.UTF8);

                return reader.ReadToEnd();
            }
            finally
            {
                // 释放资源
                if (reader != null)
                    reader.Close();
                if (stream != null)
                    stream.Close();

            }
        }

        public static List<Utxo> GetGasBalanceByAddress(string address)
        {
            JObject response = JObject.Parse(HttpGet(api + "method=getunspents&params=['" + address + "']"));
            JArray resJA = (JArray)response["result"]["balance"];

            List<Utxo> Utxos = new List<Utxo>();

            foreach (JObject jAsset in resJA)
            {
                var asset_hash = jAsset["asset_hash"].ToString();
                if (UInt256.Parse(asset_hash) != gas_hash)
                    continue;
                var jUnspent = jAsset["unspent"] as JArray;

                foreach (JObject j in jUnspent)
                {
                    try
                    {
                        Utxo utxo = new Utxo(UInt256.Parse(j["txid"].ToString()), decimal.Parse(j["value"].ToString()), int.Parse(j["n"].ToString()));
                        Utxos.Add(utxo);
                    }
                    catch
                    { }
                }
            }
            return Utxos;
        }

        public static TransactionAttribute[] GetAttribute(UInt160 scriptHash)
        {
            Random random = new Random();
            var nonce = new byte[32];
            random.NextBytes(nonce);
            TransactionAttribute[] attributes = new TransactionAttribute[]
            {
                new TransactionAttribute() { Usage = TransactionAttributeUsage.Script, Data = scriptHash.ToArray() },
                new TransactionAttribute() { Usage = TransactionAttributeUsage.Remark1, Data = nonce }
            };
            return attributes;
        }

        public static Witness[] GetWitness(byte[] signature, Neo.Cryptography.ECC.ECPoint publicKey)
        {
            var sb = new ScriptBuilder();
            sb = sb.EmitPush(signature);
            var invocationScript = sb.ToArray();

            var verificationScript = Contract.CreateSignatureRedeemScript(publicKey);
            Witness witness = new Witness() { InvocationScript = invocationScript, VerificationScript = verificationScript };

            return new[] { witness };
        }
        public class Utxo
        {
            public UInt256 txid;
            public int n;
            public decimal value;

            public Utxo(UInt256 _txid, decimal _value, int _n)
            {
                txid = _txid;
                value = _value;
                n = _n;
            }
        }
    }
}
