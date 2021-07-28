const { default: Neon, api, wallet, tx, u, sc, rpc, core } = require("@cityofzion/neon-js");
const account = new wallet.Account(
    ""
  ); //WIF
const contractHash = "edd2862dceb90b945210372d229f453f2b705f4f"; // cross Contract hash

const sb = Neon.create.scriptBuilder();
sb.emitAppCall(contractHash, "lock", [
    Neon.create.contractParam(
      "ByteArray",
      "0ef656d72483fab3804c41ea0f052dab8138da17" //nneo hash
    ),
    Neon.create.contractParam(
      "ByteArray",
      "e8e5073cf9a92e3d831921cff7c6ec60bb103b2c" //send address scripthash
    ),
    sc.ContractParam.integer("11"),
    Neon.create.contractParam(
      "ByteArray",
      "b742f4678f36887644deff6bee49185ecfee1caf"  //receiver address scripthash
    ),
    sc.ContractParam.integer("120000000") //amount
  ]);
const script2 = sb.str

// invokeTx.sign(account);
//   const script = sb.str;
let invokeTx = new tx.InvocationTransaction({ script: script2 })

//put some GAS in UTXO as network fee
let inputObj = {
  prevHash: "", // txid, utxo
  prevIndex: 0
};
let outPutObj = {
  assetId: "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7", //gas hash
  value: "80",
  scriptHash: wallet.getScriptHashFromAddress("")  //address
};
// add transaction inputs and outputs
invokeTx.inputs[0] = new tx.TransactionInput(inputObj);
invokeTx.addOutput(new tx.TransactionOutput(outPutObj));
const signature = wallet.sign(invokeTx.serialize(false), account.privateKey);
invokeTx.addWitness(tx.Witness.fromSignature(signature, account.publicKey));
const rpcClient = new rpc.RPCClient("http://seed1.ngd.network:20332");

console.log(invokeTx.hash);
rpcClient
  .sendRawTransaction(invokeTx)
  .then((response) => {
    console.log(response);
  })
  .catch((err) => {
    console.log(err);
  });
