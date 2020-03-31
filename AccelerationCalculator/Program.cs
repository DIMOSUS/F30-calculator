using System;

namespace AccelerationCalculator
{
    class Program
    {
        //const
        static readonly double w2hp = 735.49;
        static readonly double kmh2ms = 1 / 3.6;
        static readonly double TickSize = 0.001;

        static readonly bool FWD = true;
        static readonly bool RWD = true;

        //f30
        static readonly double EnginePower = 280 * w2hp;
        static readonly double Mass = 1575;
        static readonly double WellsMass = 100;//included in the mass of cars
        static readonly double FrontArea = 2.2;
        static readonly double TransmissionEfficiency = 0.78;
        static readonly double TurboLagTime = 0.0;//2.2 for n20, or zero for launch


        //zf8 45hp
        static readonly double[] GearRatios = new double[] { 4.714, 3.143, 2.106, 1.667, 1.285, 1.000, 0.839, 0.667 };
        static readonly double FirstGearShift = 6500;
        static readonly double OtherGearShift = 6800;
        static readonly double GearShiftTime = 0.15;
        static readonly double FirstGearMaxSpeed = 55 * kmh2ms;//kmh

        static readonly double Friction = 1.0;

        //BMW n20
        static double GetEnginePower(double EngineSpeed)
        {
            if (EngineSpeed < 1250)
                return EngineSpeed / 1250 * EnginePower * 0.25;
            if (EngineSpeed < 5000)
                return EngineSpeed / 5000 * EnginePower;
            return EnginePower;
        }

        static double GetEngineSpeed(int Gear, double Speed)
        {
            return Math.Max(FirstGearShift / FirstGearMaxSpeed * Speed * GearRatios[Gear - 1] / GearRatios[0], 1300);
        }

        static double AirResistance(double speed)
        {
            double x = speed;
            double Cx = 0.26;
            double p = 1.23;

            return Cx * FrontArea * p * x * x * x / 2;
        }

        static double GetWellPower(double Speed, double EngineSpeed, double turboLagTime, ref double WheelSlipTime)
        {
            double dirtPower = GetEnginePower(EngineSpeed) * TransmissionEfficiency * (turboLagTime > 0 ? 0.5 : 1.0) - AirResistance(Speed);
            //P=F*V
            double dirtForce = dirtPower / (Speed + 0.25);
            double theoreticalForceLimitAWD = Mass * 9.8 * Friction * ((RWD ? 0.75 : 0) + (FWD ? 0.25 : 0));

            if (dirtForce > theoreticalForceLimitAWD)
            {
                WheelSlipTime += TickSize;
                return dirtPower * (theoreticalForceLimitAWD / dirtForce) * 0.8;
            }

            return dirtPower;
        }

        static void Main(string[] args)
        {
            double[] times = new double[30];

            double time = 0;
            double speed = 0;
            double engineSpeed;
            double wheelSlipTime = 0;
            double distance = 0;
            double quarter = 0;
            double quarterSpeed = 0;
            double turboLagTime = TurboLagTime;
            int quarterGear = 0;

            bool needGearShift = false;
            int gear = 1;

            Console.WriteLine("km/h\ttime");

            while (speed < 250 * kmh2ms)
            {
                double speedKmH = speed * 3.6;

                if (quarter == 0 && distance > 402)
                {
                    quarter = time;
                    quarterSpeed = speedKmH;
                    quarterGear = gear;
                }

                int iSpeed = (int)Math.Floor(speedKmH * 0.1);
                if(iSpeed > 0)
                {
                    if (times[iSpeed] == 0)
                    {
                        times[iSpeed] = time;
                        if(iSpeed < 21)
                            Console.WriteLine("{0}\t{1}", iSpeed*10, Math.Round( time, 2));
                    }
                }

                if (needGearShift)
                {
                    needGearShift = false;
                    gear++;
                    time += GearShiftTime;
                    turboLagTime -= GearShiftTime;
                    distance += speed * GearShiftTime;
                    continue;
                }

                distance += speed * TickSize;


                engineSpeed = GetEngineSpeed(gear, speed);

                double prePower = (Mass + WellsMass * 0.8) * speed * speed / 2;

                double powerInc = GetWellPower(speed, engineSpeed, turboLagTime, ref wheelSlipTime) * TickSize;

                speed = Math.Sqrt(2 * (prePower + powerInc) / (Mass + WellsMass * 0.8));

                time += TickSize;
                turboLagTime -= TickSize;

                if (gear == 1)
                {
                    if (engineSpeed > FirstGearShift)
                        needGearShift = true;
                }
                else
                {
                    if (engineSpeed > OtherGearShift)
                        needGearShift = true;
                }
            }
            Console.WriteLine();
            Console.WriteLine("Quarter \t{0}", Math.Round(quarter, 2));
            Console.WriteLine("Quarter Speed \t{0}", Math.Round(quarterSpeed, 2));
            Console.WriteLine("Quarter Gear \t{0}", quarterGear);
            Console.WriteLine("100-200 \t{0}", Math.Round(times[20] - times[10], 2));
            Console.WriteLine("Wheel Slip Time {0}", Math.Round(wheelSlipTime, 2));
        }
    }
}
