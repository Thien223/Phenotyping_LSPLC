import time
import chardet
from python.cores import LSPLC, AgentC, ModbusPLC

if __name__ == '__main__':
    device = LSPLC(port="COM7", baudrate=9600, timeout=50)
    # agentC = AgentC(ip="localhost", port= 3395)

    count = 0
    while True:
        # count +=1
        # if count==1:
        #     fromVariable = "D0014"
        # elif count==2:
        #     fromVariable = "K0011"
        # elif count == 3:
        #     fromVariable = "D0014"
        # else:
        #     print("END..")
        #     device.close()
        #     break
        a = device.read(fromVariable="D0014", readLength="96")

        # the_encoding = chardet.detect(a)['encoding']
        # print(the_encoding)
        print(a)
        # print(a.decode("ASCII"))
        # agentC.sendMessage("this is message from client..\n")
