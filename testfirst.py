import serial


def close()-> None:
    if device.is_open:
        device.close()

def _open()->None:
    try:
        close()
        device.open()

        print(f"OPENNED..")
    except Exception as e:
        raise IOError(f"Open failed on port: {device.port}, error: {e}")

def read(device)->bytes:#### PLC manual 참고
    # ASCII code
    ENQ = "" ## ENQ
    nationalNumber = "5" ### 상대국번
    command = "R" ### 명령어, r = h72, R = h52
    command_type = "SB"   ### 명령어 타입
    variableSize = "65"       ### 변수 크기
    fromVariable = "%MW100"   ### 변수 이름
    readLength = "5"   ## 변수 갯수
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
    # data = f"{ENQ}{nationalNumber}{command}{command_type}{variableSize}{fromVariable}{readLength}{EOT}{BCC}"
    # data = "".join(f'{c:02x}' for c in data.encode('utf-8')).encode()
    # data = data.encode("ASCII")
    # _hex = ""
    # # _hex = "".join([hex(ch)[2:] for ch in data])
    # for ch in data:
    #     h = f"0{hex(ch)[2:]}"
    #     _hex += h[-2:]
    # #
    # print(f"ASCII command.. {data}")
    # ascii_ = bytes.fromhex(data).decode("ASCII")
    data ="05355253423635254D5731303035043635".encode()
    print(f"data: {data}")
    print(f"device is open..")
    a = device.write(data)
    print(f"wrote: {a}")
    print(f"Reading from device..")
    response = device.read_until(b"\x03")  ## read until 
    # response = device.read(1024) ## read until 
    print(f"Read from device..")
    ## decode data to string
    # data = data.decode()
    if not response:
        raise IOError(f"Read from device failed..")
    # if device.is_open:
    #     print(f"device is open..")
    #     a = device.write(data)
    #     print(f"wrote: {a}")
    #     print(f"Reading from device..")
    #     response = device.read_until(b"\x03") ## read until 
    #    # response = device.read(1024) ## read until 
    #     print(f"Read from device..")
    #     ## decode data to string
    #     # data = data.decode()
    #     if not response:
    #         raise IOError(f"Read from device failed..")
    # else:
    #     print(f"device is NOT open..")
    #     raise IOError("Device port is not open, try <device>.open() first..")
    return response

### send string to PLC
def write(data:str = None)->None:
    ### Convert strong to bytes first
    data = data.encode()
    ### data must be bytes array
    if device.is_open:
        try:
            device.write(data)
            print(f"Wrote to PLC...")
        except Exception as e:
            raise IOError(f"Write failed, error: {e}")
    else:
        raise IOError("Device port is not open, try <device>.open() first..")



if __name__=="__main__":
    device = serial.Serial()
    device.port = "COM4"
    device.stopbits = serial.STOPBITS_ONE
    device.bytesize = serial.EIGHTBITS
    device.xonxoff = False
    device.rtscts = True
    device.dsrdtr = True
    device.baudrate = 9600
    device.timeout = 1
    _open()
    read(device)