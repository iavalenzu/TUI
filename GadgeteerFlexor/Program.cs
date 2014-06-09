using System;
using System.Collections;
using System.Threading;
using System.IO;
using System.Text;

using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GTI = Gadgeteer.Interfaces;
using Gadgeteer.Modules.GHIElectronics;

namespace GadgeteerFlexor
{
    public partial class Program
    {

        private const double forceLimit = 0.80;
        private const int maxCycles = 8; //8*100 millis = 0,8 segundos
        private const int intervalMillis = 100;


        private GT.Timer _pollingTimer;

        private GTI.AnalogInput AI_ForceSensor;

        private double forceValue;
        private bool is_being_pressed; //Indica si la pelota esta siendo presionada
        private int pressed_times; //Indica el numero de veces que ha sido presionada la pelota en el intervalo
        private int cycles; //Indica el numero de ciclos transcurridos en el intervalo

        private GT.Socket Socket10;

        private string rootDirectory;

        private StreamWriter forceValuesFile;
        private StreamWriter facesFile;

        /*
         * 1 : Green
         * 2 : Blue
         * 3 : Gray
         * 4 : Red
         */

        private int currentFace;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");

            Socket10 = GT.Socket.GetSocket(10, true, null, null);
            AI_ForceSensor = new GTI.AnalogInput(Socket10, GT.Socket.Pin.Five, null);

            sdCard.SDCardMounted += new SDCard.SDCardMountedEventHandler(sdCard_SDCardMounted);
            sdCard.SDCardUnmounted += new SDCard.SDCardUnmountedEventHandler(sdCard_SDCardUnmounted);

            is_being_pressed = false;
            pressed_times = 0;
            cycles = 0;

            currentFace = 0;

            sdCard.MountSDCard();
            System.Threading.Thread.Sleep(intervalMillis);

            rootDirectory = sdCard.GetStorageDevice().RootDirectory;

            _pollingTimer = new GT.Timer(intervalMillis);
            _pollingTimer.Tick += new GT.Timer.TickEventHandler(TimerTick);
            _pollingTimer.Start();

        }

        double getForceValue(){

            double forceValue;
            
            forceValue = AI_ForceSensor.ReadVoltage();

            return forceValue;

        }

        void cambiaCara() {

            switch (currentFace)
            {
            
                case 0:
                    multicolorLed.TurnGreen();
                    currentFace = 1;
                    break;
                case 1:
                    multicolorLed.TurnBlue();
                    currentFace = 2;
                    break;
                case 2:
                    multicolorLed.TurnWhite();
                    currentFace = 3;
                    break;
                case 3:
                    multicolorLed.TurnRed();
                    currentFace = 4;
                    break;
                case 4:
                    multicolorLed.TurnGreen();
                    currentFace = 1;
                    break;
                default:
                    break;
            }
        
        }

        void guardaActualCara() {

            /*
             * El led comienza a parpadear
             */ 

            multicolorLed.BlinkRepeatedly(multicolorLed.GetCurrentColor());

            /*
             * Guardar la hora y la cara guardada
             */

            facesFile = new StreamWriter(rootDirectory + @"\FacesValues.txt", true);

            string color = "";


            if (currentFace == 1) {
                color = "Verde";
            }
            else if (currentFace == 2)
            {
                color = "Azul";
            }
            else if (currentFace == 3)
            {
                color = "Blanco";
            }
            else if (currentFace == 4)
            {
                color = "Rojo";
            }

            string line = DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + "," + color;


            facesFile.WriteLine(line);
            facesFile.Close();
        
        }

        bool isPressed() {

            /*
             * Define la logica que determina si la pelota ha sido presionada en base a las lecturas de los sensores de fuerza
             */ 

            return (forceValue <= forceLimit);
        
        }

        void apagaPelota() {

            multicolorLed.TurnOff();
            currentFace = 0;

            facesFile = new StreamWriter(rootDirectory + @"\FacesValues.txt", true);

            string line = DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + "," + "Se apaga la pelota!!";

            facesFile.WriteLine(line);
            facesFile.Close();

        
        }

        void TimerTick(GT.Timer timer)
        {
            forceValue = getForceValue();

            if (isPressed()) 
            {

                /*
                 * Se guarda el valor obtenido del sensor
                 */

                forceValuesFile = new StreamWriter(rootDirectory + @"\ForceValues.txt", true);
                forceValuesFile.WriteLine(forceValue.ToString());
                forceValuesFile.Close();

                if (is_being_pressed) 
                {
                    //La pelota sigue siendo presionada
                }
                else
                {
                    //La pelota ha sido presionada
                    is_being_pressed = true;
                }
            } 
            else 
            {
                if (is_being_pressed) 
                {
                    //La pelota ha sido liberada
                    is_being_pressed = false;

                    pressed_times++;

                    if (pressed_times >= 3)
                    {
                        Debug.Print("La pelota ha sido liberada tres veces consecutivas!!");

                        pressed_times = 0;
                        cycles = 0;

                        apagaPelota();

                    } 
                    else if (pressed_times == 2)
                    {
                        Debug.Print("La pelota ha sido liberada dos veces consecutivas!!");

                        //pressed_times = 0;
                        cycles = 0;

                        guardaActualCara();
                    }
                    else if (pressed_times == 1) 
                    {
                        Debug.Print("La pelota ha sido liberada una vez!!");
                        cycles = 0;

                        cambiaCara();
                    }


                }
                else
                { 
                    //La pelota esta en reposo
                }
            }

            if (cycles >= maxCycles)
            {
                pressed_times = 0;
                cycles = 0;
            }
            else
            {
                cycles++;
            }
            
        }

        void sdCard_SDCardUnmounted(SDCard sender)
        {
            Debug.Print("The SD card has been unmounted");
            Debug.Print("DO NOT try to access it without mounting it again first");
        }

        void sdCard_SDCardMounted(SDCard sender, GT.StorageDevice SDCard)
        {
            Debug.Print("SD card has been successfully mounted. You can now read/write/create/delete files");
            Debug.Print("Unmount before removing");
        }

    }
}
