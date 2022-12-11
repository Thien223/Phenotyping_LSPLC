import socket
import time
from threading import Thread
import minimalmodbus

from pymodbus.constants import Defaults
Defaults.RetryOnEmpty = True
Defaults.Timeout = 5
Defaults.Retries = 5
from pymodbus.utilities import computeCRC
from pymodbus.client.serial import ModbusSerialClient
import crcmod
import serial

class ModbusPLC():
    def __init__(self, port:str="COM1", baudrate:int=9600, timeout:int=1) -> None:
        self.instrument = ModbusSerialClient(port=port, baudrate=baudrate,timeout=timeout)
        self.instrument.connect()
        # Good practice
        self.instrument.close_port_after_each_call = False
        self.instrument.clear_buffers_before_each_transaction = False

    def read(self, fromVariable:str, readLength:str="1"):#### PLC manual 참고
        response = None
        # request = bytearray()
        request = ""
        request +="05"
        request +="02"
        request +="0013"
        request +="0013"
        request += str(computeCRC(request))


        # request.append(5) #### station number
        # request.append(2) ### FUNCTION CODE, 1~4: READ, 5,6,F,10: WRITE
        # request.append(0)
        # request.append(13)
        # request.append(0)
        # request.append(13)
        start = time.time()
        #
        # crc16 = computeCRC(request)

        #print(f"request: '{request}'")
        self.instrument.write_register(0,10)
        response = self.instrument.read_coils(address=0x3000, count=96)
        # response = self.instrument.read_holding_registers(address=0x000, count=12, unit=1)
        #print(f"time: {time.time()-start}")
        return response

    def close(self) -> None:
        try:
            self.instrument.close()
        except:
            pass


class LSPLC():
    def __init__(self, port:str="COM1", baudrate:int=9600, timeout:int=1) -> None:
        self.device = serial.Serial()
        self.device.port = port
        self.device.stopbits = serial.STOPBITS_ONE
        self.device.bytesize = serial.EIGHTBITS
        self.device.xonxoff = False
        self.device.rtscts = True
        self.device.dsrdtr = True
        self.device.baudrate = baudrate
        self.device.timeout = timeout
        self.open()

    def close(self)-> None:
        if self.device.is_open:
            self.device.close()

    def open(self)->None:
        try:
            self.close()
            self.device.open()

            print(f"OPENNED..")
        except Exception as e:
            raise IOError(f"Open failed on port: {self.device.port}, error: {e}")

    def read(self, fromVariable:str, readLength:str="1")->bytes:#### PLC manual 참고
        # ASCII code
        ENQ = "" ## ENQ
        nationalNumber = "05" ### 상대국번
        command = "R" ### 명령어, r = h72, R = h52
        command_type = "SB"   ### 명령어 타입
        variableSize = "65"       ### 변수 크기
        fromVariable = "%MW100"   ### 변수 이름
        readLength = "05"   ## 변수 갯수
        EOT=""           ### EOT end of text
        BCC="65"
        # hex code
        # ENQ = "05"  ## ENQ
        # nationalNumber = "3035"  ### 상대국번
        # command = "52"  ### 명령어, r = h72, R = h52
        # command_type = "h5342"  ### 명령어 타입
        # variableSize = "3035"  ### 변수 크기
        # # fromVariable = "254D57313030"   ### 변수 이름
        # fromVariable = "4430303134"   ### 변수 이름 "D0014"
        # readLength = "3936"   ## 변수 갯수
        # EOT = "04"  ### EOT end of text
        #data = f"{nationalNumber}{command}{command_type}{variableSize}{fromVariable}{readLength}".encode()

        # print(f"BCC..'{BCC}'")
        data = f"{ENQ}{nationalNumber}{command}{command_type}{variableSize}{fromVariable}{readLength}{EOT}{BCC}"
        data = data.encode('ASCII')
        # data = data.encode("ASCII")
        # _hex = ""
        # # _hex = "".join([hex(ch)[2:] for ch in data])
        # for ch in data:
        #     h = f"0{hex(ch)[2:]}"
        #     _hex += h[-2:]
        # #
        print(f"ASCII command.. {data}")
        # ascii_ = bytes.fromhex(data).decode("ASCII")

        # print(f"device is openned..")

        if self.device.is_open:
            print(f"device is open..")
            self.device.write(data)
            print(f"Reading from device..")
            response = self.device.read_until(b"\x03") ## read until 
           # response = self.device.read(1024) ## read until 
            print(f"Read from device..")
            ## decode data to string
            # data = data.decode()
            if not response:
                raise IOError(f"Read from device failed..")
        else:
            print(f"device is NOT open..")
            raise IOError("Device port is not open, try <device>.open() first..")
        return response

    ### send string to PLC
    def write(self, data:str = None)->None:
        ### Convert strong to bytes first
        data = data.encode()
        ### data must be bytes array
        if self.device.is_open:
            try:
                self.device.write(data)
                print(f"Wrote to PLC...")
            except Exception as e:
                raise IOError(f"Write failed, error: {e}")
        else:
            raise IOError("Device port is not open, try <device>.open() first..")


class AgentC():
    def __init__(self, ip:str="127.0.0.1", port:int=3395):
        self.ip = ip
        self.port = port
        self.clients = []
        self.waitingForClient()
        self.clientMessage = {}
        self.dataToSend = ""
    def waitingForClient(self)->None:
        addClientThread = Thread(target=self.addClient)
        addClientThread.daemon=True
        addClientThread.start()

    def addClient(self)->None:
        with socket.socket(family=socket.AF_INET, type=socket.SOCK_STREAM) as server:
            server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            server.bind((self.ip, self.port))
            server.listen()
            server.settimeout(1)
            while True:
                try:
                    client, address = server.accept()
                    if client not in self.clients:
                        self.clients.append(client)
                        print(f"{len(self.clients)} clients joined...")
                except Exception as e:
                    print(f"waiting for client joins...")
                except KeyboardInterrupt:
                    server.close()
                # time.sleep(5)


    def sendMessage(self) ->None:
        if len(self.clients)<=0:
            return
        ### convert data to bytes
        dataInBytes = self.dataToSend.encode()
        toRemoveClients = []
        for i in range(len(self.clients)):
            client = self.clients[i]
            if client is not None:
                try:
                    client.send(dataInBytes)
                    print(f"data sent to agentC")
                except Exception as e:
                    print(f"sending data to agentC failed, error: {e}")
                    toRemoveClients.append(i)

        for i in toRemoveClients:
            self.clients.pop(i)
            print(f"client {i+1} removed, current clients count: {len(self.clients)}")


    def listenToClients(self) -> bool:
        if len(self.clients)<=0:
            return False
        for client in self.clients:
            messagesFile = client.makefile(mode="rw", encodings="utf-8")
            for request in messagesFile:
                paramList = request.split(",")
                assert  len(paramList)==2, "AgentC have sent an invalid message..."
