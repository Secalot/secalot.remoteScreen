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
using System.Diagnostics;
using System.Text;

using NBitcoin;

namespace RemoteScreen
{
    public class BtcParserException : Exception
    {
        public BtcParserException()
        {
        }

        public BtcParserException(string message)
            : base(message)
        {
        }

        public BtcParserException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    class BtcParser
    {
        public static string ParseBitcoinTransaction(BitcoinTransactionInfo info, byte[] transaction, Int64[] inputAmounts, ref uint timeout)
        {
            string htmlOutput = "";
            Int64 totalInputAmount = 0;
            Int64 totalOutputAmount = 0;

            if (info.transactionTooBigToDisplay == true)
            {
                throw new BtcParserException("Transaction too big to display.");
            }

            Transaction tx = new Transaction(BitConverter.ToString(transaction).Replace("-", ""));

            if (tx.Inputs.Count != inputAmounts.Length)
            {
                throw new BtcParserException("Invalid transaction.");
            }

            for(int i=0; i< inputAmounts.Length; i++)
            {
                totalInputAmount += inputAmounts[i];
            }

            htmlOutput += "<b>Inputs:</b><br>";

            for(int i=0; i < tx.Inputs.Count; i++)
            {
                string prevout = tx.Inputs[i].PrevOut.ToString();

                htmlOutput += "Prevout: " + prevout + "<br>" + "Amount: " + new Money(inputAmounts[i]).ToString() + "<br><br>";
            }

            htmlOutput += "<b>Outputs:</b><br>";

            for (int i = 0; i < tx.Outputs.Count; i++)
            {
                var script = tx.Outputs[i].ScriptPubKey;
                var address = script.GetDestinationAddress(Network.Main);

                htmlOutput += "Address: " + address + "<br>" + "Amount: " + tx.Outputs[i].Value.ToString() + "<br><br>";

                totalOutputAmount += tx.Outputs[i].Value.Satoshi;
            }

            Int64 fee = totalInputAmount - totalOutputAmount;

            htmlOutput += "<b>Fee:</b><br>";
            htmlOutput += new Money(fee).ToString();

            timeout = info.remainingTime / 1000;

            return htmlOutput;
        }
    }
}
