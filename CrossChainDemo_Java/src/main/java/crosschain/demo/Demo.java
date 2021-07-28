package crosschain.demo;

import io.neow3j.contract.ContractInvocation;
import io.neow3j.contract.ContractParameter;
import io.neow3j.crypto.transaction.RawTransactionOutput;
import io.neow3j.model.types.NEOAsset;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.math.BigInteger;
import java.util.Arrays;

@SpringBootApplication
@RestController
public class Demo {

	@Autowired
	Config config;

	public static void main(String[] args) {
		SpringApplication.run(Demo.class, args);
	}

	// amount should carry the decimal of 8, that is, input 10_00000000 for 10 nneo
	// You should call the mint function first to convert utxo-neo to nneo
	@PostMapping("/migrate")
	public String migrateToken(@RequestParam long amount){
		try {
			ContractInvocation invoc = new ContractInvocation.Builder(config.neow3j())
					.contractScriptHash(config.proxyHash())
					.function("lock")
					.parameters(Arrays.asList(
							ContractParameter.hash160(config.nNeoHash()),// asset to be transferred
							ContractParameter.hash160(config.account().getScriptHash()), //sender's address in little-endian
							ContractParameter.integer(Integer.valueOf(config.getN3Id())), // N3 chainId
							ContractParameter.hash160(config.N3ReceiveAddress()), // recipient's address in little-endian
							ContractParameter.integer(BigInteger.valueOf(amount)), //asset amount to be locked
							ContractParameter.integer(Integer.valueOf(config.getProjectIndex()))
					))
					.account(config.account())
					.build()
					.sign()
					.invoke();
			return invoc.getTransaction().getTxId();
		} catch (Exception e) {
			System.out.println(e.getMessage());
		}
		return "";
	}

	@PostMapping("/mint")
	public String mintToken(@RequestParam double lockValue){
		try {
			config.account().updateAssetBalances(config.neow3j());
			ContractInvocation invoc = new ContractInvocation.Builder(config.neow3j())
					.contractScriptHash(config.nNeoHash())
					.function("mintTokens")
					.account(config.account())
					.output(new RawTransactionOutput(NEOAsset.HASH_ID, lockValue, config.nNeoHash().toAddress()))
					.build()
					.sign()
					.invoke();
			return invoc.getTransaction().getTxId();
		}catch (Exception e) {
			System.out.println(e.getMessage());
		}
		return "";
	}
}
