using System;
using System.Reflection.Emit;

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
        static readonly double EnginePower = 430 * w2hp;
        static readonly double Mass = 1650;//(With Wells)
        static readonly double WellsMass = 100;//Included in the mass of cars
        static readonly double FrontArea = 2.4;
        static readonly double TransmissionEfficiency = 0.83;
        static readonly double TurboLagTime = 0.0;//2.2 for n20 without launch


        //zf8 45hp
        static readonly double[] GearRatios = new double[] { 4.714, 3.143, 2.106, 1.667, 1.285, 1.000, 0.839, 0.667 };
        static readonly double FirstGearShift = 6500;
        static readonly double OtherGearShift = 6700;
        static readonly double LaunchEngSpeed = 2500;
        static readonly double GearShiftTime = 0.15;
        static readonly double FirstGearMaxSpeed = 52 * kmh2ms;//kmh
        static readonly double GearСlutchStart = 0.7;

        static readonly double EngSpeedPerSec = 20000 * TickSize;

        static readonly double Friction = 1.05;

        //BMW n20
        static readonly (double, double)[] EngPower = new (double, double)[] {
            (0, 0),
            (700, 0),
            (1250, EnginePower/4),
            (4750, EnginePower*0.95),
            //(5000, EnginePower),
            (5250, EnginePower),
            (6500, EnginePower),
            (7000, EnginePower*0.94)
        };

        static double LerpData((double, double)[] data, double inValue)
        {
            if (inValue >= data[^1].Item1)
                return data[^1].Item2;

            for (int i = data.Length - 1; i > -1; i--)
            {
                if (inValue > data[i].Item1)
                {
                    double lval = (inValue - data[i].Item1) / (data[i + 1].Item1 - data[i].Item1);
                    return lerp(data[i].Item2, data[i + 1].Item2, lval);
                }
            }
            return 0;
        }

        static double GetEnginePower(double EngineSpeed)
        {
            return LerpData(EngPower, EngineSpeed);

            double maxNM0 = 550;
            double maxNM1 = 525;

            (double, double)[] data_ = new (double, double)[] {
            (0, 0),
            (1250, maxNM0),
            (3750, maxNM0),
            (4250, maxNM1),
            (5800, maxNM1),
            (6750, 450),
            (7000, 400)
        };

            double nm = LerpData(data_, EngineSpeed);
            return nm * EngineSpeed / 7187 * w2hp;
        }

        static double GetEngineSpeed(int Gear, double Speed)
        {
            return Math.Max(FirstGearShift / FirstGearMaxSpeed * Speed * GearRatios[Gear - 1] / GearRatios[0], 900);
        }

        static double AirResistance(double speed)
        {
            double x = speed;
            double Cx = 0.26;
            double p = 1.23;

            return Cx * FrontArea * p * x * x * x / 2;
        }

        static double KWellResistance(double speed)
        {
            return 0.01 * (1 + 5.5E-4 * speed * speed);
        }

        static double GetWellPower(double Speed, double Acceleration_g, double EngineSpeed, double PowerCrr, ref bool WheelSlip, Graph g_dsc, double time)
        {
            double Fk = KWellResistance(Speed);

            double dirtPower = GetEnginePower(EngineSpeed) * TransmissionEfficiency * PowerCrr;

            //P=F*V
            double dirtForce = (dirtPower) / (Speed + 0.7);
            double theoreticalForceLimitAWD = Mass * 9.8 * Friction;
            if (RWD && !FWD)
            {
                double rw_ballance = 0.28 * Acceleration_g;
                theoreticalForceLimitAWD *= 0.5 + rw_ballance;
            }
            if (!RWD && FWD)
            {
                double fw_ballance = 0.2 * Acceleration_g;
                theoreticalForceLimitAWD *= 0.5 - fw_ballance;
            }

            bool preWheelSleep = WheelSlip;

            if (dirtForce > theoreticalForceLimitAWD)
                WheelSlip = true;
            else
                WheelSlip = false;

            double k = saturate((dirtForce / theoreticalForceLimitAWD - 1)*1000);
            k = lerp(1, 0.7, k);
            g_dsc.Point(time, k);


            if (WheelSlip)// && preWheelSleep)
                dirtPower *= (theoreticalForceLimitAWD / dirtForce) * k;

            dirtPower *= 1 - Fk;

            dirtPower -= AirResistance(Speed);

            return dirtPower;
        }

        static void Main(string[] args)
        {
            Graph g_speed = new Graph("speed", 500, 300, 20, 200);
            Graph g_e_speed = new Graph("e_speed", 500, 300, 20, 7000, 1, 1000 );
            Graph g_weel_pow = new Graph("weel_pow", 500, 300, 20, EnginePower / w2hp, 1, 50 );
            Graph g_acc = new Graph("acceleration", 500, 300, 20, 2, 1, 1 );
            Graph g_e_pwr = new Graph("e_pwr", 500, 300, 7000, EnginePower / w2hp * 1.1, 1000, 50 );
            Graph g_dsc = new Graph("DSC", 500, 300, 20, 1, 1, 1);

            double[] times = new double[50];

            double time = 0;
            double speed = 0;
            double engineSpeed = LaunchEngSpeed;
            double wheelSlipTime = 0;
            double distance = 0;
            double quarter = 0;
            double quarterSpeed = 0;
            double turboLagTime = TurboLagTime;
            double acceleration_g = 0;
            double pre_acceleration_g = 0;
            double gearСlutch = GearСlutchStart;
            int quarterGear = 0;
            bool wheelSlip = false;

            double powerInc = 0;

            bool needGearShift = false;
            int gear = 1;

            for (int i = 0; i < 7000; i++)
                g_e_pwr.Point(i, GetEnginePower(i) / w2hp);

            Console.WriteLine("km/h\ttime");

            while (time < 0.1 || acceleration_g > 0.001)
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

                    g_speed.Line(time, speedKmH, time + GearShiftTime, speedKmH);
                    g_e_speed.Line(time, engineSpeed, time + GearShiftTime, GetEngineSpeed(gear, speed));
                    g_acc.Line(time - TickSize, acceleration_g, time + GearShiftTime, acceleration_g);


                    time += GearShiftTime;
                    turboLagTime -= GearShiftTime;
                    distance += speed * GearShiftTime;
                    continue;
                }

                double preESpeed = engineSpeed;

                engineSpeed = GetEngineSpeed(gear, speed);

                if (engineSpeed > preESpeed + EngSpeedPerSec)
                    engineSpeed = preESpeed + EngSpeedPerSec;
                if (engineSpeed < preESpeed - EngSpeedPerSec || wheelSlip)//DSC
                    engineSpeed = preESpeed - EngSpeedPerSec;


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

                double prePower = (Mass + WellsMass * 0.8) * speed * speed / 2;

                double prwCrr = 1;
                //gearСlutch
                if (gearСlutch > 0)
                {
                    prwCrr *= (GearСlutchStart / gearСlutch);

                    engineSpeed = lerp(engineSpeed, LaunchEngSpeed, (gearСlutch / GearСlutchStart));
                    gearСlutch -= TickSize;
                }

                prwCrr *= (turboLagTime > 0 ? 0.5 : 1.0);

                powerInc = powerInc * 0.95 + 0.05*GetWellPower(speed, acceleration_g, engineSpeed, prwCrr, ref wheelSlip, g_dsc, time);

                double new_speed = Math.Sqrt(2 * (prePower + powerInc * TickSize) / (Mass + WellsMass * 0.8));

                pre_acceleration_g = acceleration_g;
                acceleration_g = (new_speed - speed) / TickSize / 9.8;
                acceleration_g = pre_acceleration_g * 0.98 + acceleration_g * 0.02;

                speed = new_speed;

                if (wheelSlip) wheelSlipTime += TickSize;
                time += TickSize;
                turboLagTime -= TickSize;
                distance += speed * TickSize;

                g_speed.Point(time, speedKmH);
                g_e_speed.Point(time, engineSpeed);
                g_weel_pow.Point(time, powerInc / w2hp);
                g_acc.Line(time - TickSize, pre_acceleration_g, time, acceleration_g);
            }


            Console.WriteLine("" );
            Console.WriteLine("Max Speed \t{0}", Math.Round(speed * 3.6, 2));
            Console.WriteLine("Quarter \t{0}", Math.Round(quarter, 2));
            Console.WriteLine("Quarter Speed \t{0}", Math.Round(quarterSpeed, 2));
            Console.WriteLine("Quarter Gear \t{0}", quarterGear);
            if (times[20] > 0)
                Console.WriteLine("100-200 \t{0}", Math.Round(times[20] - times[10], 2));
            Console.WriteLine("Wheel Slip Time {0}", Math.Round(wheelSlipTime, 2));

            g_speed.Save();
            g_e_speed.Save();
            g_weel_pow.Save();
            g_acc.Save();
            g_e_pwr.Save();
            g_dsc.Save();
        }

        static double lerp(double v0, double v1, double t)
        {
            return (1 - t) * v0 + t * v1;
        }

        static double smoothstep(double x, double edge0 = 0, double edge1 = 1)
        {
            // Scale, bias and saturate x to 0..1 range
            x = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            // Evaluate polynomial
            return x * x * (3 - 2 * x);
        }

        static double clamp(double x, double lowerlimit, double upperlimit)
        {
            if (x < lowerlimit)
                x = lowerlimit;
            if (x > upperlimit)
                x = upperlimit;
            return x;
        }

        static double saturate(double x)
        {
            return clamp(x, 0, 1);
        }
    }
}
