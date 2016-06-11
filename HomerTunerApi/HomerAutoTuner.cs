using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HomerAutoTunerApi
{
    public enum ON
    {
        off = 0,
        on = 1
    } 

    /*
    Motors Data include
     motor positions
     motors status
    Motors status contains various information about current state of the motors (e.g. whether moving or in desired positions or if an error occurred).
    Motor positions may be either
     Actual Positions (AP) or
     Tune Positions (TP).

    One MDO may contain either Homer Measurement Results or Motors Data or both.
    Homer Status Byte is always present in MDO
     */

    public class HomerAutoTuner
    {

        private struct MDO
        {

            public byte HST;
            public byte HER;
            public byte PH;
            public byte PL;
            public byte PE;
            public byte TL;
            public byte TH;
            public byte RE;
            public byte XL;
            public byte XH;
            public byte YL;
            public byte YH;
            public byte F0;
            public byte F1;
            public byte F2;
            public byte F3;
            public byte DXL;
            public byte DXH;
            public byte DYL;
            public byte DYH;
            public byte SRL;
            public byte SRH;
            public byte M1L;
            public byte M1H;
            public byte M2L;
            public byte M2H;
            public byte M3L;
            public byte M3H;
            public byte MS1;
            public byte MS2;
            public byte CS;
        }

        private enum HSTBits
        {
            mdo_included = 0x10, // 0 Motors Data are not included , 1 Motors Data are included
            mde_period = 0x20, // 0 The MDO is a periodically sent data object 1 The MDO is a response to motor query or a single-shot command
            hmr_included = 0x4 // 2 0 Homer Measurement Results are not included 1 Homer Measurement Results are included            
        }

        byte[] writeBuf = new byte[1000];
        byte[] readData;
        // from the hoComProto55_1.pdf
        bool m_continuesMeasurement = true;
        private enum CommandCode : byte
        {
            msAtOFF = 0, //Switch continuous autotuning OFF
            msAtON = 1, //Switch continuous autotuning ON
            msAtSingle = 2, //Perform one autotuning step with the latest measured data
            msCanSetAtPar = 3,  //Only CAN: Set parameters governing autotuning behavior
            msConfirm = 4, //End message of command execution confirmation
            msGetAtune = 5,   //Query continuous autotuning status
            msSetFdelta = 6, //Set frequency tolerance
            msSetFsubst = 7, //Set substitute frequency
            msReset = 8, //  Homer resets 
            LF = 10,//Line Feed
            CR = 13, //Carriage Return

            msMeasObject = 16, //  End message of Measurement Data Object (MDO)
            msSweepStart = 17,// Start measurement
            msSweepStop = 18, // Stop measurement
            msMotStop = 19,
            //Hard stop of motors
            msDataBegin = 28,  //Message indicating that the following will be data bytes
            msSrvHalt = 34, //        Terminate Homer measurement program (Server), start file transfer program
            msFetchLast = 39, //  Instruct Homer to send the latest Measurement Results
            msHmWform = 53, // Switch signal waveform mode of operation (CW, Rectified, Pulsed)
            msHmCounter = 56, // Set frequency counter
            msHmAvrg = 57, //Set Homer averaging numbers
            msGetTmouts = 61,

            //Get Homer measurement timeout and motors movement timeout
            msMotInit = 69, //Initialize all motors
            msOneHome = 70,

            //Initialize one motor
            msMotSet = 71,

            //Set motor positions
            msATunCmd = 72,
            //Only RS232: Autotune commands
            msATunPar = 73,//RS232: Set or read parameters governing autotuning behavior

            msMotRead = 74,

            //Read motor positions
            msSetFsmplCw = 75,//Set sampling frequency
            msMotRefresh = 76,
            //Motors Refresh period
            msSrvRst = 80,
            //Terminate and restart Homer measurement program (Server)
            msClrFifo = 84, //Clear Homer FIFO
            msMeas = 85,// Make one measurement
            msCompStubs = 86, //Compute stub positions to achieve impedance match
            msMeaComp = 87,//  Measure and compute stub positions to achieve impedance match
            msMeaTun = 88, //Measure and make one autotuning step
            msMeaTunMea = 89, //Measure, make one autotuning step, measure again
            msYesSendTune = 90, //Send Tune Positions during continuous measurement
            msNoSendTune = 91,
            msSetStbSwap = 92,
            msHmSetOther = 94,
            CmndLbl = 128

        }

        private SerialPort USB_PORT = new SerialPort();
        bool m_connected = false;

        string m_serAddr;
        int m_BaudRate;
        public HomerAutoTuner(string serAddr, int BaudRate = 9600)
        {
            m_serAddr = serAddr;
            m_BaudRate = BaudRate;
        }
        public void Close()
        {
            if (m_connected)
            {
                USB_PORT.Close();
                m_connected = false;
            }
        }

        public bool Connect()
        {
            try
            {
                return connect(m_serAddr, m_BaudRate);
            }
            catch (Exception err)
            {
                throw (new SystemException(err.Message));
            }
        }
        private bool connect(string serAddr, int BaudRate = 9600)
        {
            try
            {
                USB_PORT.Close(); // close any existing handle
                USB_PORT.BaudRate = BaudRate;
                USB_PORT.PortName = serAddr;
                USB_PORT.DataBits = 8;
                USB_PORT.StopBits = StopBits.One;
                USB_PORT.ReadTimeout = 2000;
                USB_PORT.WriteTimeout = 2000;
                USB_PORT.Parity = Parity.None;
                USB_PORT.Open();
                m_connected = USB_PORT.IsOpen;
                return m_connected;
            }
            catch (Exception err)
            {
                throw (new SystemException(err.Message));
            }
        }

        private bool Write(byte[] SerBuf)
        {
            try
            {
                if (m_connected == false)
                    return false;
                USB_PORT.Write(SerBuf, 0, SerBuf.Length);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsPortOpen()
        {
            return USB_PORT.IsOpen;
        }

        private bool Write(byte[] SerBuf, int size)
        {
            try
            {
                if (m_connected == false)
                    return false;
                byte[] d = { 0 };
                USB_PORT.Write(SerBuf, 0, size);
                return true;
            }
            catch (Exception err)
            {
                throw (new SystemException(err.Message));
            }
        }

        private byte[] Read(int numBytes)
        {

            if (m_connected == false)
                throw (new SystemException("Not connected"));

            readData = new byte[numBytes];

            // this will call the read function for the passed number times, 
            // this way it ensures each byte has been correctly recieved while still using timeouts
            for (int i = 0; i < numBytes; i++)
            {
                try
                {
                    USB_PORT.Read(readData, i, 1);

                }
                catch (Exception err)
                {
                    throw (new SystemException(err.Message));
                }
            }
            return readData;
        }
        /*
        Two cases must be distinguished: the data byte (let us denote it DataByte) differing from CmndLbl = 128 = 80h and data byte equal to CmndLbl.
         If DataByte differs from CmndLbl, it is simply transmitted as a single byte.
         If DataByte = CmndLbl, it is doubled, i.e. sent as two consecutive identical bytes:
        DataByte, DataByte
        i.e.
        128, 128
        Example: To send the 5-byte data stream 1, 2, 128, 3, 4, the following six bytes must be transmitted:
        1, 2, 128, 128, 3, 4
         */
        int SendCommand(CommandCode command)
        {
            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)command;
            return i;
        }
        /*
         3.2.4 Data Objects
        Transfer of data (howsoever they may be interpreted) between Homer and an external controller is realized by means of Data Objects, which are variable-length byte sequences consisting of three consecutive components:
        1. Data Begin message, which is a two-byte sequence: CmndLbl = 128 = 80h and msDataBegin = 28 = 1Ch, i.e. 128,28.
        2. Data itself (note that each byte equal to CmndLbl must be doubled).
        3. End Message, which is a two-byte sequence CmndLbl,EndMsg, where EndMsg is data identifier, indicating that Data stream has ended and how it should be interpreted.
        Example: To send the data stream 30, 128, 40 with end message code 99, the following data sequence must be transmitted:
        128, 28, 30, 128, 128, 40, 128, 99
        Data bytes are indicated by boldface; note the doubling of byte 128.
        */

        int SendDataObject(byte[] data, int i)
        {
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            for (int j = 0; j < data.Length; j++)
            {
                writeBuf[i++] = data[j];
                if (data[j] == 128)
                {
                    writeBuf[i++] = (byte)CommandCode.CmndLbl;
                }
            }
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = 99;
            return i;
        }
        public void Autotuning(bool onoff)
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }

            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            if (onoff == false)
            {
                string cmd = "ATC 0";
                byte[] array = Encoding.ASCII.GetBytes(cmd);
                i = SendDataObject(array, i);
                //msAtOFF=0 To switch autotuning OFF
            }
            else
            {
                //msAtON = 1 To switch autotuning ON
                string cmd = "ATC 1";
                byte[] array = Encoding.ASCII.GetBytes(cmd);
                i = SendDataObject(array, i);
            }

            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

        }
        //msGetAtune=5 To query autotuning state (Server version V54 and more only)

        public void SingleAutotuningStep()
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }
            if (m_continuesMeasurement == true)
            {
                throw (new SystemException("Continues is enabled , turn it off"));
            }
            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            string cmd = "ATC S";
            byte[] array = Encoding.ASCII.GetBytes(cmd);
            i = SendDataObject(array, i);

            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));


        }
        void PrintResponse(Tuple<byte[], int> t)
        {
            for (int i = 0; i < t.Item2; i++)
            {
                Console.WriteLine(t.Item1[i]);
            }
        }
        public void SelectedStubHome(byte stubNumber)
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }

            if (stubNumber < 1 || stubNumber > 3)
            {
                throw (new SystemException("Invalid stub number , 1 , 2 ,3 are allow"));
            }
            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            string cmd = "M1H " + stubNumber;
            byte[] array = Encoding.ASCII.GetBytes(cmd);
            i = SendDataObject(array, i);

            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

            readResponse();

        }
        //Write: 128, 28, 77, 80, 79, 32, 48, 32, 53, 49, 51, 32, 52, 48, 48, 48, 13, 10, 128, 71
        //Response:128, 28, 48, 0, 0, 1, 2, 160, 15, 119, 0, 89, 128, 16
        public void SetMotorPositions(int motor1Pos, int motor2Pos, int motor3Pos)
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }

            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

         

            string cmd = string.Format("MPO {0} {1} {2}", motor1Pos, motor2Pos, motor3Pos);
            byte[] array = Encoding.ASCII.GetBytes(cmd);

            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            for (int j = 0; j < array.Length; j++)
            {
                writeBuf[i++] = array[j];
                if (array[j] == 128)
                {
                    writeBuf[i++] = (byte)CommandCode.CmndLbl;
                }
            }
            writeBuf[i++] = 13;
            writeBuf[i++] = 10;

            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msMotSet;

            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

            readResponseObject();

          


        }
        public void HardStopMotors()
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }

            int i = SendCommand(CommandCode.msMotStop);
            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

        }
        byte[] receiveMDO()
        {

            byte hst = ReadByte();
            if (((hst & (byte)HSTBits.mdo_included) == 1) && ((hst & 0x20) == 0x20))
            {
                byte b = 0;
                int i = 0;
                while (b != (byte)CommandCode.msMeasObject)
                {
                    try
                    {
                        USB_PORT.Read(readData, i, 1);
                        i++;
                    }
                    catch (Exception e)
                    {
                        throw (new SystemException("Error reading from I2C " + e.Message));
                    }
                }
                byte[] response = new byte[i - 2];
                for (int j = 0; j < response.Length; j++)
                {
                    response[j] = readData[j];
                }
                return response;
            }
            else
            {
                return null;
            }
        }
        // Command: 128, 74
        // Response example (motors 1, 2, 3 set to 0, 513, and 4000 steps, respectively):
        // 128, 28, 48, 0, 0, 1, 2, 160, 15, 119, 0, 89, 128, 16
        public void ReadMotorPositions(out ushort stub1, 
                                       out ushort stub2,
                                       out ushort stub3)
        {   
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }
            int i = SendCommand(CommandCode.msMotRead);
            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));


             readResponseObjectForRead(out stub1, out stub2, out stub3);


        }
        public void AllStubsHome()
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }
            int i = SendCommand(CommandCode.msMotInit);
            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

            readResponse();
         
           
        }
        public void SendTunePositionsONOFF(ON b)
        {
            if (m_connected == false)
            {
                throw (new SystemException("No Connected"));
            }
            int i = 0;
            writeBuf[i++] = (byte)CommandCode.CmndLbl;
            writeBuf[i++] = (byte)CommandCode.msDataBegin;

            if (b == ON.on)
            {
                string cmd = "ATC T";
                byte[] array = Encoding.ASCII.GetBytes(cmd);
                i = SendDataObject(writeBuf, i);
            }
            else
            {
                string cmd = "ATC F";
                byte[] array = Encoding.ASCII.GetBytes(cmd);
                i = SendDataObject(writeBuf, i);
            }
            if (Write(writeBuf, i) == false)
                throw (new SystemException("Error write to com"));

        }


        private byte ReadByte()
        {
            if (m_connected == false)
                throw (new SystemException("Not connected"));
            // this will call the read function for the passed number times, 
            // this way it ensures each byte has been correctly recieved while still using timeouts
            try
            {
                int data = USB_PORT.ReadByte();
                return (byte)data;
            }
            catch (Exception e)
            {
                throw (new SystemException("Error reading from I2C " + e.Message));
            } // timeout or other error occured, set lost comms indicator
        }

        private void readResponse()
        {
            int i = 0;
            byte data;
           
            data = ReadByte();
            while (data != (byte)CommandCode.CmndLbl)
            {
                data = ReadByte();
            }
            if (data != (byte)CommandCode.CmndLbl)
            {
                 
            }
            else
            {
                if (data == (byte)CommandCode.CmndLbl)
                {
                    data = ReadByte();
                    if (data != (byte)CommandCode.CmndLbl)
                    {
                        while (data != (byte)CommandCode.msConfirm)
                        {
                            data = ReadByte();
                           
                            
                        }
                        
                    }
                    else
                    {
                        throw (new SystemException("Error 889"));
                    }
                }
                else  {
                    throw (new SystemException("Error 889"));
                }
            }

          
        }

        private void readResponseObjectForRead(out ushort stub1,
                                                out ushort stub2, 
                                                out ushort stub3)
        {
            
            byte data;

            stub1 = 0;
            stub2 = 0;
            stub3 = 0;
            
            data = ReadByte();
            while (data != (byte)CommandCode.CmndLbl)
            {
                data = ReadByte();
            }
            if (data != (byte)CommandCode.CmndLbl)
            {
                throw (new SystemException("Error in read response object , byte need to be 128"));
            }
            else
            {
                if (data == (byte)CommandCode.CmndLbl)
                {
                    
                    data = ReadByte();
                    if (data != (byte)CommandCode.CmndLbl)
                    {
                        int j = 0;

                        bool first16 = true;
                        bool first48 = true;
                        while (data != (byte)CommandCode.msMeasObject)
                        {
                            data = ReadByte();
                            if (first16 == true && data == 16)
                            {
                                first16 = false;
                                data = ReadByte();
                            }
                            first16 = false;
                            if (first48 == true && data == 48)
                            {
                                first48 = false;
                                continue;
                            }
                                                        
                            if (j == 0)
                            {
                                stub1 = data;
                            } else
                            if (j == 1)
                            {
                                stub1 += (ushort)(data * 256);
                            }
                            else
                            if (j == 2)
                            {
                                stub2 = data;
                            }
                            else
                            if (j == 3)
                            {
                                stub2 += (ushort)(data * 256);
                            }
                            else
                            if (j == 4)
                            {
                                stub3= data;
                            }
                            else
                            if (j == 5)
                            {
                                stub3 += (ushort)(data * 256);
                            }
                            j++;
                        }
                    }
                    else
                    {
                        throw (new SystemException("Unhandled in read "));
                    }
                }
                else  {
                    throw (new SystemException("Unhandled in read 222"));
                }
            }

          
        }

        private void readResponseObject()
        {
            int i = 0;
            byte data;
            byte[] buf = new byte[150];
            data = ReadByte();
            while (data != (byte)CommandCode.CmndLbl)
            {
                data = ReadByte();
            }
            if (data != (byte)CommandCode.CmndLbl)
            {
                throw (new SystemException("Error in read response object , byte need to be 128"));
            }
            else
            {
                if (data == (byte)CommandCode.CmndLbl)
                {
                    byte prevdata = 0;
                    data = ReadByte();
                    if (data != (byte)CommandCode.CmndLbl)
                    {
                       
                        while (data != (byte)CommandCode.msMeasObject)
                        {
                            data = ReadByte();
                            if (data == 16 && prevdata != 128)
                            {
                                data = ReadByte();
                                continue;
                            }
                            prevdata = data;
                            if (data == 128)
                                continue;
                            if (data == (byte)CommandCode.msMeasObject)
                                continue;
                             
                        }
                    }
                    else
                    {
                        throw (new SystemException("Error 111"));
                    }
                }
                else  
                {
                      throw (new SystemException("Error 2222"));
                }
            }
        }

        private bool Read(int numBytes, byte[] readData)
        {
            if (m_connected == false)
                return false;
            // this will call the read function for the passed number times, 
            // this way it ensures each byte has been correctly recieved while still using timeouts
            for (int i = 0; i < numBytes; i++)
            {
                try
                {
                    USB_PORT.Read(readData, i, 1);
                }
                catch (Exception e)
                {
                    throw (new SystemException("Error reading from I2C " + e.Message));
                } // timeout or other error occured, set lost comms indicator
            }
            return true;
        }
    }


}
