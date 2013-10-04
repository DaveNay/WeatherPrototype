using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace NetduinoPlusTesting
{
    /// <summary>
    /// Bosch BMP085 digital pressure and temperature sensor.
    /// </summary>
    public class BMP085
    {
        private I2CDevice.Configuration _slaveConfig; // BMP085 I2CDevice configuration
        private const byte ClockRateKHz = 40; // BMP085 clock rate
        private byte _oversamplingSetting; // Oversampling for measurements.  Please see the datasheet for this sensor for more information.
        private readonly byte[] _pressureWaitTime = new byte[] {5, 8, 14, 26}; // These wait times correspond to the oversampling settings.  Please see the datasheet for this sensor for more information.
        private const int TransactionTimeout = 1000; //ms
        private Timer _sensorTimer;

        public enum DeviceMode
        {
            UltraLowPower = 0,
            Standard = 1,
            HighResolution = 2,
            UltraHighResolution = 3
        }

        // Calibration data backing stores
        private short _ac1;
        private short _ac2;
        private short _ac3;
        private ushort _ac4;
        private ushort _ac5;
        private ushort _ac6;
        private short _b1;
        private short _b2;
        private short _mb;
        private short _mc;
        private short _md;

        public BMP085(byte address, DeviceMode deviceMode)
        {
            Address = address;
            _slaveConfig = new I2CDevice.Configuration(address, ClockRateKHz);
            _oversamplingSetting = (byte) deviceMode;

            // Get calibration data that will be used for future measurement taking.
            GetCalibrationData();

            // Take initial measurements.
            TakeMeasurements();

            // Take new measurements every 30 seconds.
            _sensorTimer = new Timer(TakeMeasurements, null, 200, 30000);
        }

        /// <summary>
        /// Calculates the compensated pressure and temperature.
        /// </summary>
        private void TakeMeasurements()
        {
            TakeMeasurements(null);
        }

        /// <summary>
        /// Calculates the compensated pressure and temperature.
        /// </summary>
        /// <param name="state"></param>
        private void TakeMeasurements(object state)
        {
            long x1, x2, x3, b3, b4, b5, b6, b7, p;

            long ut = ReadUncompensatedTemperature();

            long up = ReadUncompensatedPressure();

            // calculate the compensated temperature
            x1 = (ut - _ac6) * _ac5 >> 15;
            x2 = (_mc << 11) / (x1 + _md);
            b5 = x1 + x2;
            _celsius = (float)((b5 + 8) >> 4) / 10;

            // calculate the compensated pressure
            b6 = b5 - 4000;
            x1 = (_b2 * (b6 * b6 >> 12)) >> 11;
            x2 = _ac2 * b6 >> 11;
            x3 = x1 + x2;
            switch (_oversamplingSetting)
            {
                case 0:
                    b3 = ((_ac1 * 4 + x3) + 2) >> 2;
                    break;
                case 1:
                    b3 = ((_ac1 * 4 + x3) + 2) >> 1;
                    break;
                case 2:
                    b3 = ((_ac1 * 4 + x3) + 2);
                    break;
                case 3:
                    b3 = ((_ac1 * 4 + x3) + 2) << 1;
                    break;
                default:
                    throw new Exception("Oversampling setting must be 0-3");
            }
            x1 = _ac3 * b6 >> 13;
            x2 = (_b1 * (b6 * b6 >> 12)) >> 16;
            x3 = ((x1 + x2) + 2) >> 2;
            b4 = (_ac4 * (x3 + 32768)) >> 15;
            b7 = (up - b3) * (50000 >> _oversamplingSetting);
            p = (b7 < 0x80000000 ? (b7 * 2) / b4 : (b7 / b4) * 2);
            x1 = (p >> 8) * (p >> 8);
            x1 = (x1 * 3038) >> 16;
            x2 = (-7357 * p) >> 16;
            _pascal = (int) (p + ((x1 + x2 + 3791) >> 4));
        }

        private long ReadUncompensatedTemperature()
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[2] { 0xF4, 0x2E }, TransactionTimeout);

            // Required as per datasheet.
            Thread.Sleep(5);

            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[] { 0xF6 }, TransactionTimeout);

            // get MSB and LSB result
            byte[] inputData = new byte[2];
            I2CBus.GetInstance().Read(_slaveConfig, inputData, TransactionTimeout);

            return ((inputData[0] << 8) | inputData[1]);
        }

        private long ReadUncompensatedPressure()
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[2] { 0xF4, (byte) (0x34+(_oversamplingSetting<<6)) }, TransactionTimeout);

            // insert pressure waittime using oversampling setting as index.
            Thread.Sleep(_pressureWaitTime[_oversamplingSetting]);

            // get MSB and LSB result
            byte[] inputData = new byte[3];
            I2CBus.GetInstance().ReadRegister(_slaveConfig, 0xF6, inputData, TransactionTimeout);

            return ((inputData[0] << 16) | (inputData[1] << 8) | (inputData[2])) >> (8 - _oversamplingSetting);
        }

        /// <summary>
        /// Retrieves the factory calibration data stored in the sensor.
        /// </summary>
        private void GetCalibrationData()
        {
            _ac1 = ReadShort(0xAA);
            _ac2 = ReadShort(0xAC);
            _ac3 = ReadShort(0xAE);
            _ac4 = (ushort)ReadShort(0xB0);
            _ac5 = (ushort)ReadShort(0xB2);
            _ac6 = (ushort)ReadShort(0xB4);
            _b1 = ReadShort(0xB6);
            _b2 = ReadShort(0xB8);
            _mb = ReadShort(0xBA);
            _mc = ReadShort(0xBC);
            _md = ReadShort(0xBE);
        }

        private short ReadShort(byte registerAddress)
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[] { registerAddress }, TransactionTimeout);

            // get MSB and LSB result
            byte[] inputData = new byte[2];
            I2CBus.GetInstance().Read(_slaveConfig, inputData, TransactionTimeout);

            return (short)((inputData[0] << 8) | inputData[1]);
        }

        private byte _address;
        public byte Address
        {
            get { return _address; }
            private set { _address = value; }
        }

        private int _pascal;
        public int Pascal
        {
            get { return _pascal; }
        }

        public float InchesMercury
        {
            get
            {
                return (float) (_pascal / 3386.389);
            }
        }

        private float _celsius;
        public float Celsius
        {
            get { return _celsius; }
        }

        public void Dispose()
        {
            _sensorTimer.Dispose();
        }

        public float Fahrenheit
        {
            get
            {
                return (float) (((_celsius*9)/5) + 32.0);
            }
        }
    }
}
