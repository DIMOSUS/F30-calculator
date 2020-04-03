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
        static readonly double Mass = 1630;//(With Wells)
        static readonly double WellsMass = 100;//Included in the mass of cars
        static readonly double FrontArea = 2.2;
        static readonly double TransmissionEfficiency = 0.82;
        static readonly double TurboLagTime = 0.0;//2.2 for n20 without launch


        //zf8 45hp
        static readonly double[] GearRatios = new double[] { 4.714, 3.143, 2.106, 1.667, 1.285, 1.000, 0.839, 0.667 };
        static readonly double FirstGearShift = 6500;
        static readonly double OtherGearShift = 6700;
        static readonly double LaunchEngSpeed = 2500;
        static readonly double GearShiftTime = 0.2;
        static readonly double FirstGearMaxSpeed = 52 * kmh2ms;//kmh
        static readonly double DSCReaction = 1500;//eng speed per sec
        static readonly double GearСlutchStart = 0.4;

        static readonly double Friction = 1.0;

        //BMW n20
        static readonly (double, double)[] EngPower = new (double, double)[] {
            (0, 0),
            (700, 0),
            (1250, EnginePower/4),
            (5000, EnginePower),
            (6500, EnginePower),
            (7000, EnginePower*0.94)
        };

        static double GetEnginePower(double EngineSpeed)
        {
            if (EngineSpeed >= EngPower[^1].Item1)
                return EngPower[^1].Item2;

            for (int i = EngPower.Length - 1; i > -1; i--)
            {
                if(EngineSpeed > EngPower[i].Item1)
                {
                    double lval = (EngineSpeed - EngPower[i].Item1) / (EngPower[i + 1].Item1 - EngPower[i].Item1);
                    return lerp(EngPower[i].Item2, EngPower[i + 1].Item2, lval);
                }
            }
            return 0;
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

        static double GetWellPower(double Speed, double Acceleration_g, double EngineSpeed, double turboLagTime, ref bool WheelSlip)
        {
            double Fk = 0.01 * (1 + 5.5E-4 * Speed * Speed);

            double dirtPower = GetEnginePower(EngineSpeed) * TransmissionEfficiency * (turboLagTime > 0 ? 0.5 : 1.0);
            dirtPower *= 1 - Fk;

            //P=F*V
            double dirtForce = dirtPower / (Speed + 0.1);
            double theoreticalForceLimitAWD = Mass * 9.8 * Friction;// * ((RWD ? 0.75 : 0) + (FWD ? 0.25 : 0));
            if (RWD && !FWD)
            {
                double rw_ballance = 0.35 * Acceleration_g;
                theoreticalForceLimitAWD *= 0.5 + rw_ballance;
            }
            if (!RWD && FWD)
            {
                double fw_ballance = 0.2 * Acceleration_g;
                theoreticalForceLimitAWD *= 0.5 - fw_ballance;
            }


            double slipK = Math.Min((dirtForce / theoreticalForceLimitAWD - 1) / 0.3, 1);

            if (slipK > 0)
            {
                WheelSlip = true;
                return dirtPower * (theoreticalForceLimitAWD / dirtForce) * lerp(1, 0.8, slipK);
            }
            else
                WheelSlip = false;

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

            double[] times = new double[30];

            double time = 0;
            double speed = 0;
            double engineSpeed = 0;
            double wheelSlipTime = 0;
            double distance = 0;
            double quarter = 0;
            double quarterSpeed = 0;
            double turboLagTime = TurboLagTime;
            double acceleration_g = 0;
            double gearСlutch = GearСlutchStart;
            double dscCorr = 0;
            int quarterGear = 0;
            bool wheelSlip = false;

            bool needGearShift = false;
            int gear = 1;

            for (int i = 0; i < 7000; i++)
            {
                g_e_pwr.Point(i, GetEnginePower(i) / w2hp);
            }

            Console.WriteLine("km/h\ttime");

            while (speed < 230 * kmh2ms)
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

                    time += GearShiftTime;
                    turboLagTime -= GearShiftTime;
                    distance += speed * GearShiftTime;
                    continue;
                }

                engineSpeed = GetEngineSpeed(gear, speed);

                //gearСlutch
                if (gearСlutch > 0)
                    engineSpeed = lerp(engineSpeed, LaunchEngSpeed, smoothstep(gearСlutch / GearСlutchStart));
                gearСlutch -= TickSize;

                //DSC
                if (wheelSlip)
                    dscCorr += DSCReaction * TickSize;
                else
                    dscCorr = Math.Max(0, dscCorr - DSCReaction * TickSize * 3);
                engineSpeed -= dscCorr;

                double prePower = (Mass + WellsMass * 0.8) * speed * speed / 2;

                double powerInc = GetWellPower(speed, acceleration_g, engineSpeed, turboLagTime, ref wheelSlip);

                double new_speed = Math.Sqrt(2 * (prePower + powerInc * TickSize) / (Mass + WellsMass * 0.8));

                acceleration_g = (new_speed - speed) / TickSize / 9.8;

                speed = new_speed;

                if (wheelSlip) wheelSlipTime += TickSize;
                time += TickSize;
                turboLagTime -= TickSize;
                distance += speed * TickSize;

                g_speed.Point(time, speedKmH);
                g_e_speed.Point(time, engineSpeed);
                g_weel_pow.Point(time, powerInc / w2hp);
                g_acc.Point(time, acceleration_g);

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

            g_speed.Save();
            g_e_speed.Save();
            g_weel_pow.Save();
            g_acc.Save();
            g_e_pwr.Save();
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
    }
}
