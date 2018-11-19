/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using Nethereum.RLP;
using Org.BouncyCastle.Math;

namespace RemoteScreen
{
    public class EthParserException : Exception
    {
        public EthParserException()
        {
        }

        public EthParserException(string message)
            : base(message)
        {
        }

        public EthParserException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    class EthParser
    {

        public static string ParseEthereumTransaction(EthereumTransactionInfo info, byte[] input, ref uint timeout)
        {
            string htmlOutput = "";
            string details = "";

            Dictionary<int, string> chainIDDict = new Dictionary<int, string>()
            {
                { 1, "Ethereum mainnet"},
                { 2, "Morden(disused), Expanse mainnet"},
                { 3, "Ropsten"},
                { 4, "Rinkeby"},
                { 30, "Rootstock mainnet"},
                { 31, "Rootstock testnet"},
                { 42, "Kovan"},
                { 61, "Ethereum Classic mainnet"},
                { 62, "Ethereum Classic testnet"},
                { 1337, "Geth private chains (default)"},
            };

            if (info.transactionTooBigToDisplay == true)
            {
                throw new EthParserException("Transaction too big to display.");
            }

            if (info.type == 0x6666)
            {
                throw new EthParserException("A message is being signed. Cannot display.");
            }
            else
            {
                RLPCollection collection = RLP.Decode(input);

                if (collection.Count != 1)
                {
                    throw new EthParserException("Invalid transaction.");
                }

                collection = (RLPCollection)collection[0];

                if (collection.Count != 9)
                {
                    throw new EthParserException("Invalid transaction.");
                }

                string fromAddress = "0x" + string.Join(string.Empty, Array.ConvertAll(info.address, b => b.ToString("x2")));

                string nonce = new BigInteger(collection[0].RLPData).ToString(10);

                string gasPrice = "";
                var gasPriceInGwei = new BigInteger(collection[1].RLPData).DivideAndRemainder(new BigInteger("1000000000"));
                var gasPriceInGweiRemainder = gasPriceInGwei[1].ToString(10).TrimEnd('0');

                if (gasPriceInGweiRemainder == "")
                {
                    gasPrice = gasPriceInGwei[0].ToString(10) + " GWEI";
                }
                else
                {
                    gasPrice = gasPriceInGwei[0].ToString(10) + "." + gasPriceInGweiRemainder + " GWEI";
                }

                string gasLimit = new BigInteger(collection[2].RLPData).ToString(10);
                string toAddress = "0x" + string.Join(string.Empty, Array.ConvertAll(collection[3].RLPData, b => b.ToString("x2")));

                string value = "";
                var valueInEth = new BigInteger(collection[4].RLPData).DivideAndRemainder(new BigInteger("1000000000000000000"));
                var valueInEthRemainder = valueInEth[1].ToString(10).TrimEnd('0');

                if(valueInEthRemainder == "")
                {
                    value = valueInEth[0].ToString(10) + " ETH";
                }
                else
                {
                    value = valueInEth[0].ToString(10) + "." + valueInEthRemainder + " ETH";
                }

                

                string data = "None";
                if (collection[5].RLPData != null)
                {
                    data = "0x" + string.Join(string.Empty, Array.ConvertAll(collection[5].RLPData, b => b.ToString("x2")));
                }

                int v = collection[6].RLPData.ToIntFromRLPDecoded();

                string chainID = "";

                if (chainIDDict.ContainsKey(v))
                {
                    chainID = chainIDDict[v];
                }
                else
                {
                    chainID = v.ToString();
                }

                if ((collection[7].RLPData != null) || (collection[8].RLPData != null))
                {
                    throw new EthParserException("Invalid transaction.");
                }

                details += "<b>Send to: </b>" + toAddress + "<br>";
                details += "<b>Send from: </b>" + fromAddress + "<br>";
                details += "<b>Value: </b>" + value + "<br>";
                details += "<b>ChainID: </b>" + chainID + "<br>";
                details += "<b>Gas limit: </b>" + gasLimit + "<br>";
                details += "<b>Gas price: </b>" + gasPrice + "<br>";
                details += "<b>Nonce: </b>" + nonce + "<br>";
                details += "<b>Data: </b>" + data;
                
            }

            htmlOutput += details;

            timeout = info.remainingTime / 1000;

            return htmlOutput;
        }
    }
}
