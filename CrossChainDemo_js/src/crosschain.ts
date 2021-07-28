const { default: Neon, api, wallet, tx, u, sc, rpc, core } = require("@cityofzion/neon-js"); // "@cityofzion/neon-js": "^4.9.0"
const account = new wallet.Account(
    "L5e41j9s6JkeoEtpusv5Z8kfjufWYoRpoLcz3R8eieSATaFeQ9p3"
  ); //WIF
const wrapperHash = "7997ac991b66ca3810602639a2f2c1bd985e8b5a"; // wrapper contract hash on neo2

const sb = Neon.create.scriptBuilder();
sb.emitAppCall(wrapperHash, "lock", [
    sc.ContractParam.hash160("17da3881ab2d050fea414c80b3fa8324d756f60e"), //nneo hash
    sc.ContractParam.byteArray(account.address, "address"), //send address scripthash
    sc.ContractParam.integer("88"), // N3 chainId
    sc.ContractParam.hash160("d0b83be2ea17a6c020e9ed189c4db9b93fe18e0e"), //receiver address scripthash
    sc.ContractParam.integer("1100000000"), //amount, >= 10 nNEO, free; otherwise, need cGAS as fee
    sc.ContractParam.integer("10086"), // projectIndex
  ]);
const script2 = sb.str

let invokeTx = new tx.InvocationTransaction({ script: script2, gas: 0 })

invokeTx.addAttribute(
    tx.TxAttrUsage.Script,
    u.reverseHex(wallet.getScriptHashFromAddress(account.address))
  );

 invokeTx.addRemark(
    Date.now().toString() + u.ab2hexstring(u.generateRandomArray(4))
  );

const signature = wallet.sign(invokeTx.serialize(false), account.privateKey);
invokeTx.addWitness(tx.Witness.fromSignature(signature, account.publicKey));
const rpcClient = new rpc.RPCClient("http://seed1.ngd.network:20332");

console.log(invokeTx.hash);
rpcClient
  .sendRawTransaction(invokeTx)
  .then(response => {
    console.log(response);
  })
  .catch(err => {
    console.log(err);
  });