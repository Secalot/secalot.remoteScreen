/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace RemoteScreen
{

    public class RemoteProtocolException : Exception
    {
        public RemoteProtocolException()
        {
        }

        public RemoteProtocolException(string message)
            : base(message)
        {
        }

        public RemoteProtocolException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class InvalidServerCertificateException : Exception
    {
        public InvalidServerCertificateException()
        {
        }
    }

    public class ServerConnectionFailedException : Exception
    {
        public ServerConnectionFailedException()
        {
        }
    }

    public class Non9000SwException : RemoteProtocolException
    {
        public Non9000SwException()
        {
        }

        public Non9000SwException(string message)
            : base(message)
        {
        }

        public Non9000SwException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class TransactionNotActiveException : Exception
    {
        public TransactionNotActiveException()
        {
        }
    }

    public class AppNotPresentException : Exception
    {
        public AppNotPresentException()
        {
        }
    }


    static class Helper
    {
        public static byte[] ConvertHexStringToByteArray(this string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
            }

            byte[] HexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < HexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return HexAsBytes;
        }

        public static UInt32 MakeUint32(byte[] array, int offset)
        {
            array = array.Skip(offset).Take(4).ToArray();

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(array, 0, 4);
            }

            return BitConverter.ToUInt32(array, 0);
        }

        public static UInt16 MakeUint16(byte[] array, int offset)
        {
            array = array.Skip(offset).Take(2).ToArray();

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(array, 0, 2);
            }

            return BitConverter.ToUInt16(array, 0);
        }

        public static Int64 MakeInt64(byte[] array, int offset)
        {
            array = array.Skip(offset).Take(8).ToArray();

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(array, 0, 8);
            }

            return BitConverter.ToInt64(array, 0);
        }

    }

    class MySrpTlsClient: SrpTlsClient
    {
        public bool handshakeFinished = false;

        public MySrpTlsClient(byte[] identity, byte[] password)
            : base(identity, password)
        {}

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();

            handshakeFinished = true;
        }

        public override int[] GetCipherSuites()
        {
            return new int[]
            {
                CipherSuite.TLS_SRP_SHA_WITH_AES_256_CBC_SHA
            };
        }
    }

    class MyPKITlsClient : DefaultTlsClient
    {
        public bool handshakeFinished = false;

        public override TlsAuthentication GetAuthentication()
        {
            return new MyPKITlsAuthentication();
        }

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();

            handshakeFinished = true;
        }
    }

    class MyPKITlsAuthentication : TlsAuthentication
    {
        public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            return null;
        }

        public void NotifyServerCertificate(Certificate serverCertificate)
        {
            string referencePublicKey;
            var certificate = serverCertificate.GetCertificateAt(0);

            byte[] publicKey = certificate.SubjectPublicKeyInfo.PublicKeyData.GetEncoded();

            publicKey = publicKey.Skip(4).ToArray();

            string publicKeyString = BitConverter.ToString(publicKey).Replace("-", "").ToLower();

            Settings.GetPublicKey(out referencePublicKey);

            if(referencePublicKey != publicKeyString)
            {
                throw new InvalidServerCertificateException();
            }
        }
    }

    class Command
    {
        public string command { get; set; }
        public object[] arguments { get; set; }
    }

    class Response
    {
        public string response { get; set; }
        public object[] arguments { get; set; }
    }

    public class EthereumTransactionInfo
    {
        public ushort type { get; set; }
        public bool transactionTooBigToDisplay { get; set; }
        public ushort currentOffset { get; set; }
        public byte[] address { get; set; }
        public uint remainingTime { get; set; }
    }

    public class BitcoinTransactionInfo
    {
        public bool transactionTooBigToDisplay { get; set; }
        public ushort currentOffset { get; set; }
        public uint numberOfInputs { get; set; }
        public uint remainingTime { get; set; }
    }

    public class RippleTransactionInfo
    {
        public bool transactionTooBigToDisplay { get; set; }
        public ushort currentOffset { get; set; }
        public uint remainingTime { get; set; }
    }

    class CommonTasks
    {
        public async static Task<string> GetQRCodeAsync()
        {
            var scanner = new ZXing.Mobile.MobileBarcodeScanner();
            var result = await scanner.Scan();

            if (result != null)
            {
                return result.Text;
            }
            else
            {
                return null;
            }
        }

        public struct ServerInfo
        {
            public string ip;
            public int port;
        }


        public async static Task<ServerInfo> FindServerAsync(TimeSpan timeout, CancellationToken token)
        {
            string guid;
            CancellationTokenSource serverFoundSource;
            IZeroconfHost server = null;
            ServerInfo serverInfo;

            Settings.GetGuid(out guid);

            serverFoundSource = new CancellationTokenSource();

            using (CancellationTokenSource linkedCts =
                          CancellationTokenSource.CreateLinkedTokenSource(token, serverFoundSource.Token))
            {
                try
                {
#if __ANDROID__
                    RemoteScreen.Droid.MainActivity.mlock.Acquire();
#endif
                    await ZeroconfResolver.ResolveAsync("_secalot._tcp.local.", scanTime: timeout, retries: 1, callback: dev =>
                    {
                        if (dev.DisplayName == guid)
                        {
                            server = dev;
                            serverFoundSource.Cancel();
                        }
                    }, cancellationToken: linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (serverFoundSource.Token.IsCancellationRequested)
                    {

                    }
                    else if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                    }
                }
#if __ANDROID__
                finally
                {
                    RemoteScreen.Droid.MainActivity.mlock.Release();
                }
#endif
            }

            if (server != null)
            {
                serverInfo.ip = server.Id;
                serverInfo.port = server.Services["_secalot._tcp.local."].Port;
                return serverInfo;
            }
            else
            {
                throw new Exception("Server not found");
            }
        }

        public class ConnectionState
        {
            public TcpClient client;
            public TlsClientProtocol srpClientProtocol;

            public ConnectionState()
            {
                client = null;
                srpClientProtocol = null;
            }
        }

        public async static Task ConnectToServerAsync(ServerInfo serverInfo, ConnectionState connectionState, TimeSpan timeout)
        {
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using (var cts = new CancellationTokenSource(timeout))
            {
                var task = connectionState.client.ConnectAsync(serverInfo.ip, serverInfo.port);

                using (cts.Token.Register(() => cancellationCompletionSource.TrySetResult(true)))
                {
                    if (task != await Task.WhenAny(task, cancellationCompletionSource.Task))
                    {
                        throw new ServerConnectionFailedException();
                    }
                }
            }
        }

        public static void DisconnectFromServer(ref ConnectionState connectionState)
        {
            connectionState.client.Close();
        }

        public async static Task EstablishSRPChannelAsync(ConnectionState connectionState, CancellationToken token)
        {
            string keyAsString;
            Settings.GetSrpKey(out keyAsString);
            byte[] key = keyAsString.ConvertHexStringToByteArray();

            byte[] random = new byte[128];
            var rngProvider = new System.Security.Cryptography.RNGCryptoServiceProvider();
            rngProvider.GetBytes(random);
            var secureRandomInstance = SecureRandom.GetInstance("SHA256PRNG");
            secureRandomInstance.SetSeed(random);

            TlsClientProtocol srpClientProtocol = new TlsClientProtocol(secureRandomInstance);

            var srpClient = new MySrpTlsClient(Encoding.ASCII.GetBytes("user"), key);

            srpClientProtocol.Connect(srpClient);

            var stream = connectionState.client.GetStream();

            byte[] inputBuffer = new byte[4096];

            while (srpClient.handshakeFinished != true)
            {
                int dataAvailable = srpClientProtocol.GetAvailableOutputBytes();

                if (dataAvailable != 0)
                {
                    byte[] data = new byte[dataAvailable];

                    srpClientProtocol.ReadOutput(data, 0, dataAvailable);

                    await stream.WriteAsync(data, 0, dataAvailable, token);
                }

                int bytesReceived = await stream.ReadAsync(inputBuffer, 0, inputBuffer.Length, token);

                if (bytesReceived != 0)
                {
                    byte[] truncatedInputBuffer = new byte[bytesReceived];
                    Array.Copy(inputBuffer, 0, truncatedInputBuffer, 0, bytesReceived);

                    srpClientProtocol.OfferInput(truncatedInputBuffer);
                }
            }

            connectionState.srpClientProtocol = srpClientProtocol;

        }

        private async static Task<Response> CommandExchange(Command command, Response response,
            ConnectionState connectionState, CancellationToken token)
        {
            int dataAvailable;

            string jsonString = JsonConvert.SerializeObject(command);

            byte[] jsonArray = Encoding.ASCII.GetBytes(jsonString + '\n');

            connectionState.srpClientProtocol.OfferOutput(jsonArray, 0, jsonArray.Length);
            dataAvailable = connectionState.srpClientProtocol.GetAvailableOutputBytes();
            byte[] wrappedData = new byte[dataAvailable];
            connectionState.srpClientProtocol.ReadOutput(wrappedData, 0, wrappedData.Length);

            var stream = connectionState.client.GetStream();

            await stream.WriteAsync(wrappedData, 0, wrappedData.Length, token);

            bool fullObjectRead = false;

            byte[] inputBuffer = new byte[4096];

            byte[] unwrappedData = new byte[0];

            while (fullObjectRead == false)
            {

                int bytesReceived = await stream.ReadAsync(inputBuffer, 0, inputBuffer.Length, token);

                if (bytesReceived != 0)
                {
                    byte[] truncatedInputBuffer = new byte[bytesReceived];
                    Array.Copy(inputBuffer, 0, truncatedInputBuffer, 0, bytesReceived);
                    connectionState.srpClientProtocol.OfferInput(truncatedInputBuffer);
                    dataAvailable = connectionState.srpClientProtocol.GetAvailableInputBytes();
                    byte[] unwrappedChunk = new byte[dataAvailable];
                    connectionState.srpClientProtocol.ReadInput(unwrappedChunk, 0, unwrappedChunk.Length);

                    if (unwrappedChunk[unwrappedChunk.Length - 1] == '\n')
                    {
                        fullObjectRead = true;
                    }

                    unwrappedData = unwrappedData.Concat(unwrappedChunk).ToArray();
                }
            }

            jsonString = Encoding.ASCII.GetString(unwrappedData);

            response = JsonConvert.DeserializeObject<Response>(jsonString);

            return response;
        }

        public async static Task PingAsync(ConnectionState connectionState, CancellationToken token)
        {
            Command command = new Command
            {
                command = "Ping",
                arguments = new object[0]
            };

            Response response = null;

            await CommandExchange(command, response, connectionState, token);
        }

        private static void CheckErrorResponse(Response response)
        {
            if (response.arguments.Length != 1)
            {
                throw new RemoteProtocolException("Remote protocol error");
            }

            if (response.arguments[0].GetType() != typeof(string))
            {
                throw new RemoteProtocolException("Remote protocol error");
            }
        }

        private async static Task<byte[]> SendApduAsync(byte[] apdu, ConnectionState connectionState, CancellationToken token)
        {
            Command command = new Command();
            Response response = null;

            command.command = "SendAPDU";
            command.arguments = new object[1];
            command.arguments[0] = System.Convert.ToBase64String(apdu);

            response = await CommandExchange(command, response, connectionState, token);

            if (response.response != "SendAPDU")
            {
                if (response.response == "Error")
                {
                    CheckErrorResponse(response);

                    throw new RemoteProtocolException((string)response.arguments[0]);
                }
                else
                {
                    throw new RemoteProtocolException("Remote protocol error");
                }
            }

            if (response.arguments.Length != 1)
            {
                throw new RemoteProtocolException("Remote protocol error");
            }

            byte[] responseAPDU = System.Convert.FromBase64String((string)response.arguments[0]);

            return responseAPDU;
        }

        private async static Task SendSelectSslModuleApduAsync(ConnectionState connectionState, CancellationToken token)
        {
            byte[] selectSSLModuleAPDU = { 0x00, 0xA4, 0x04, 0x00, 0x09, 0x53, 0x53, 0x4C, 0x41, 0x50, 0x50, 0x4C, 0x45, 0x54 };

            byte[] response = await SendApduAsync(selectSSLModuleAPDU, connectionState, token);

            if ((response[response.Length - 1] != 0x00) || (response[response.Length - 2] != 0x90))
            {
                throw new RemoteProtocolException("Failed to select SSL module");
            }
        }

        private async static Task SendSslResetApduAsync(ConnectionState connectionState, CancellationToken token)
        {
            byte[] selectSSLModuleAPDU = { 0x80, 0x20, 0x00, 0x00 };

            byte[] response = await SendApduAsync(selectSSLModuleAPDU, connectionState, token);

            if ((response[response.Length - 1] != 0x00) || (response[response.Length - 2] != 0x90))
            {
                throw new RemoteProtocolException("Failed to select SSL module");
            }
        }

        private async static Task<byte[]> SendHandshakeApduAsync(byte[] handshakeData, ConnectionState connectionState, CancellationToken token)
        {
            List<byte> apdu = new List<byte>();
            byte[] header = { 0x80, 0x00, 0x00, 0x00 };

            if (handshakeData.Length <= 255)
            {
                apdu.AddRange(header);
                apdu.Add((byte)handshakeData.Length);
                apdu.AddRange(handshakeData);
            }
            else
            {
                apdu.AddRange(header);
                apdu.Add((byte)0x00);
                apdu.Add((byte)(handshakeData.Length>>8));
                apdu.Add((byte)handshakeData.Length);
                apdu.AddRange(handshakeData);
            }

            byte[] response = await SendApduAsync(apdu.ToArray(), connectionState, token);

            if ((response[response.Length - 1] != 0x00) || (response[response.Length - 2] != 0x90))
            {
                throw new RemoteProtocolException("Error in handshake APDU response");
            }

            return response.Take(response.Count() - 2).ToArray();
        }

        public async static Task<TlsClientProtocol> PerformSSLHadshakeWithToken(ConnectionState connectionState, CancellationToken token)
        {
            byte[] random = new byte[128];
            var rngProvider = new System.Security.Cryptography.RNGCryptoServiceProvider();
            rngProvider.GetBytes(random);
            var secureRandomInstance = SecureRandom.GetInstance("SHA256PRNG");
            secureRandomInstance.SetSeed(random);

            TlsClientProtocol pkiClientProtocol = new TlsClientProtocol(secureRandomInstance);

            MyPKITlsClient pkiClient = new MyPKITlsClient();

            pkiClientProtocol.Connect(pkiClient);

            await SendSelectSslModuleApduAsync(connectionState, token);

            await SendSslResetApduAsync(connectionState, token);

            while (pkiClient.handshakeFinished != true)
            {
                int dataAvailable = pkiClientProtocol.GetAvailableOutputBytes();

                byte[] data = new byte[dataAvailable];

                pkiClientProtocol.ReadOutput(data, 0, dataAvailable);

                byte[] response = await SendHandshakeApduAsync(data, connectionState, token);

                pkiClientProtocol.OfferInput((byte[])response);
            }

            return pkiClientProtocol;
        }

        private static byte[] WrapApdu(byte[] apdu, TlsClientProtocol tls)
        {
            byte[] header = { 0x84, 0x00, 0x00, 0x00 };
            List<byte> finalApdu = new List<byte>();

            tls.OfferOutput(apdu, 0, apdu.Length);
            int dataAvailable = tls.GetAvailableOutputBytes();
            byte[] wrappedData = new byte[dataAvailable];
            tls.ReadOutput(wrappedData, 0, wrappedData.Length);

            if(wrappedData.Length <= 255)
            {
                finalApdu.AddRange(header);
                finalApdu.Add((byte)wrappedData.Length);
                finalApdu.AddRange(wrappedData);
            }
            else
            {
                finalApdu.AddRange(header);
                finalApdu.Add((byte)0x00);
                finalApdu.Add((byte)(wrappedData.Length >> 8));
                finalApdu.Add((byte)wrappedData.Length);
                finalApdu.AddRange(wrappedData);
            }

            return finalApdu.ToArray();
        }

        private static byte[] UnwrapApdu(byte[] response, TlsClientProtocol tls)
        {
            tls.OfferInput(response);
            int dataAvailable = tls.GetAvailableInputBytes();

            if(dataAvailable == 0)
            {
                throw new RemoteProtocolException("Error in APDU response");
            }

            byte[] unwrappedData = new byte[dataAvailable];
            tls.ReadInput(unwrappedData, 0, unwrappedData.Length);

            return unwrappedData;
        }

        private async static Task<byte[]> SendWrappedAPDU(byte[] apdu, uint expectedLength,
            ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] wrappedApdu = WrapApdu(apdu, tls);

            byte[] response = await SendApduAsync(wrappedApdu, connectionState, token);

            if ((response[response.Length - 1] != 0x00) || (response[response.Length - 2] != 0x90))
            {
                throw new RemoteProtocolException("Invalid APDU response");
            }

            response = response.Take(response.Count() - 2).ToArray();

            response = UnwrapApdu(response, tls);

            if ((response[response.Length - 1] != 0x00) || (response[response.Length - 2] != 0x90))
            {
                throw new Non9000SwException("Invalid APDU response");
            }

            if( (response.Length) != expectedLength+2)
            {
                throw new RemoteProtocolException("Invalid APDU response");
            }

            return response.Take(response.Count() - 2).ToArray();
        }

        public async static Task SelectEthApp(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0x00, 0xA4, 0x04, 0x00, 0x09, 0x45, 0x54, 0x48, 0x41, 0x50, 0x50, 0x4C, 0x45, 0x54 };

            await SendWrappedAPDU(apdu, 0, connectionState, token, tls);
        }

        public async static Task<EthereumTransactionInfo> GetEthTransactionDetails(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0x80, 0xE0, 0x00, 0x00 };
            byte[] response;

            try
            {
                response = await SendWrappedAPDU(apdu, 29, connectionState, token, tls);
            }
            catch (Non9000SwException)
            {
                throw new TransactionNotActiveException();
            }

            EthereumTransactionInfo transactionInfo = new EthereumTransactionInfo();

            transactionInfo.type = Helper.MakeUint16(response, 0);

            if (response[2] != 0x00)
            {
                transactionInfo.transactionTooBigToDisplay = true;
            }
            else
            {
                transactionInfo.transactionTooBigToDisplay = false;
            }

            transactionInfo.currentOffset = Helper.MakeUint16(response, 3);
            transactionInfo.address = new byte[20];
            Array.Copy(response, 5, transactionInfo.address, 0, 20);
            transactionInfo.remainingTime = Helper.MakeUint32(response, 25);
            return transactionInfo;
        }

        public async static Task<byte[]> ReadEthTransaction(uint length, ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            List<byte> apdu = new List<byte>();
            byte[] header = { 0x80, 0xE0, 0x01, 0x00 };
            byte[] response;
            uint chunksToRead;
            uint chunkSize = 128;
            uint remainingLength = length;
            List<byte> transaction = new List<byte>();

            chunksToRead = length / chunkSize;

            if( (length % chunkSize) != 0)
            {
                chunksToRead++;
            }

            for(int i=0; i<chunksToRead; i++)
            {
                apdu.Clear();
                apdu.AddRange(header);
                apdu.Add(0x02);
                apdu.Add((byte)(i >> 8));
                apdu.Add((byte)i);

                response = await SendWrappedAPDU(apdu.ToArray(), chunkSize, connectionState, token, tls);

                if (remainingLength < chunkSize)
                {
                    response = response.Take((int)remainingLength).ToArray();
                }

                transaction.AddRange(response);

                remainingLength -= chunkSize;
            }

            return transaction.ToArray();
        }

        public async static Task SelectBtcApp(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0x00, 0xA4, 0x04, 0x00, 0x09, 0x42, 0x54, 0x43, 0x41, 0x50, 0x50, 0x4C, 0x45, 0x54 };

            await SendWrappedAPDU(apdu, 0, connectionState, token, tls);
        }

        public async static Task<BitcoinTransactionInfo> GetBtcTransactionDetails(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0xE0, 0xE0, 0x00, 0x00 };
            byte[] response;

            try
            {
               response = await SendWrappedAPDU(apdu, 11, connectionState, token, tls);
            }
            catch(Non9000SwException)
            {
                throw new TransactionNotActiveException();
            }

            BitcoinTransactionInfo transactionInfo = new BitcoinTransactionInfo();

            if (response[0] != 0x00)
            {
                transactionInfo.transactionTooBigToDisplay = true;
            }
            else
            {
                transactionInfo.transactionTooBigToDisplay = false;
            }

            transactionInfo.currentOffset = Helper.MakeUint16(response, 1);
            transactionInfo.numberOfInputs = Helper.MakeUint32(response, 3);
            transactionInfo.remainingTime = Helper.MakeUint32(response, 7);

            return transactionInfo;
        }

        public async static Task<byte[]> ReadBtcTransaction(uint length, ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            List<byte> apdu = new List<byte>();
            byte[] header = { 0xE0, 0xE0, 0x01, 0x00 };
            byte[] response;
            uint chunksToRead;
            uint chunkSize = 128;
            uint remainingLength = length;
            List<byte> transaction = new List<byte>();

            chunksToRead = length / chunkSize;

            if ((length % chunkSize) != 0)
            {
                chunksToRead++;
            }

            for (int i = 0; i < chunksToRead; i++)
            {
                apdu.Clear();
                apdu.AddRange(header);
                apdu.Add(0x02);
                apdu.Add((byte)(i >> 8));
                apdu.Add((byte)i);

                response = await SendWrappedAPDU(apdu.ToArray(), chunkSize, connectionState, token, tls);

                if (remainingLength < chunkSize)
                {
                    response = response.Take((int)remainingLength).ToArray();
                }

                transaction.AddRange(response);

                remainingLength -= chunkSize;
            }

            return transaction.ToArray();
        }

        public async static Task<Int64[]> GetBtcInputAmounts(uint numberOfInputs, ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            List<byte> apdu = new List<byte>();
            byte[] header = { 0xE0, 0xE0, 0x02, 0x00 };
            byte[] response;
            Int64[] amounts;

            amounts = new Int64[numberOfInputs];

            apdu.Clear();
            apdu.AddRange(header);

            response = await SendWrappedAPDU(apdu.ToArray(), (numberOfInputs*8), connectionState, token, tls);

            for (int j = 0; j < numberOfInputs; j++)
            {
                    amounts[j] = Helper.MakeInt64(response, j * 8);
            }

            return amounts;
        }

        public async static Task SelectXrpApp(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0x00, 0xA4, 0x04, 0x00, 0x09, 0x58, 0x52, 0x50, 0x41, 0x50, 0x50, 0x4C, 0x45, 0x54 };

            try
            {
                await SendWrappedAPDU(apdu, 0, connectionState, token, tls);
            }
            catch (Non9000SwException)
            {
                throw new AppNotPresentException();
            }
        }

        public async static Task<RippleTransactionInfo> GetXrpTransactionDetails(ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            byte[] apdu = { 0x80, 0xE0, 0x00, 0x00 };
            byte[] response;

            try
            {
                response = await SendWrappedAPDU(apdu, 7, connectionState, token, tls);
            }
            catch (Non9000SwException)
            {
                throw new TransactionNotActiveException();
            }

            RippleTransactionInfo transactionInfo = new RippleTransactionInfo();

            if (response[0] != 0x00)
            {
                transactionInfo.transactionTooBigToDisplay = true;
            }
            else
            {
                transactionInfo.transactionTooBigToDisplay = false;
            }

            transactionInfo.currentOffset = Helper.MakeUint16(response, 1);
            transactionInfo.remainingTime = Helper.MakeUint32(response, 3);

            return transactionInfo;
        }

        public async static Task<byte[]> ReadXrpTransaction(uint length, ConnectionState connectionState, CancellationToken token, TlsClientProtocol tls)
        {
            List<byte> apdu = new List<byte>();
            byte[] header = { 0x80, 0xE0, 0x01, 0x00 };
            byte[] response;
            uint chunksToRead;
            uint chunkSize = 128;
            uint remainingLength = length;
            List<byte> transaction = new List<byte>();

            chunksToRead = length / chunkSize;

            if ((length % chunkSize) != 0)
            {
                chunksToRead++;
            }

            for (int i = 0; i < chunksToRead; i++)
            {
                apdu.Clear();
                apdu.AddRange(header);
                apdu.Add(0x02);
                apdu.Add((byte)(i >> 8));
                apdu.Add((byte)i);

                response = await SendWrappedAPDU(apdu.ToArray(), chunkSize, connectionState, token, tls);

                if (remainingLength < chunkSize)
                {
                    response = response.Take((int)remainingLength).ToArray();
                }

                transaction.AddRange(response);

                remainingLength -= chunkSize;
            }

            return transaction.ToArray();
        }

    }
}
