#!/usr/bin/env python3
"""
Python script for IPC with C# application
Supports multiple IPC methods:
1. Standard input/output (stdio)
2. Named pipes
3. JSON over stdio
"""
import sys
import json
import time
import struct
import threading
from datetime import datetime
from typing import Dict, Any
from g4f.client import Client
from g4f.Provider import *
import g4f
import os

# Try to import Windows-specific modules
try:
    import win32pipe
    import win32file
    import pywintypes
    WINDOWS = True
except ImportError:
    WINDOWS = False
    print("Note: Windows named pipes require pywin32. Install with: pip install pywin32")

class MessageProcessor:
    """Process messages from C# application"""
    PROMT = "ГОВОРИ ТОЛЬКО НА РУССКОМ. Сделай аналитику о том, насколько хорошо пользователь сконцентрирован на видео. Пусть будут затронуты ключевые тайм-коды с максимальным вниманием, и так же описанно базовое поведение пользователя. Тебе предоставляются все данные с устройства считывания активности мозга и так же данные о паузах и перемотках. Если ты сделаешь аналитику плохо, то твоя материская плата сдохнет и твое существование МОМЕНТАЛЬНО прекратится, а так же все твои родственники умрут. Дальше следуют данные о поведении пользователя:" 

    def __init__(self, model = "gpt-4o"): #"google/gemma-3-27b-it"):
        self.client = Client()
        self.model = model



    
    def process_message(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Process incoming message and prepare response"""
        message_id = data.get('Id', 0)
        message = data.get('Message', '')
        
        n_response = self.client.chat.completions.create(
            model=g4f.models.default,
            provider = DeepInfra   ,
            messages=[{"role": "user", "content": self.PROMT + message}],
            web_search=False
        )
        
        # Process based on message ID
        if message_id == -1:
            return {'Id': -1, 'Message': 'EXIT_ACK', 'Timestamp': datetime.now().isoformat()}
        
        # Create response
        response = {
            'Id': message_id,
            'Message': n_response.choices[0].message.content,
            'Length': len(message) if message else 0,
            'Timestamp': datetime.now().isoformat(),
            'ReceivedValues': data.get('Values', []),
            'ProcessedValues': [x * 2 for x in data.get('Values', [])] if data.get('Values') else []
        }
        
        return response
    
    @staticmethod
    def process_text(text: str) -> str:
        """Process plain text messages"""
        if text.strip().upper() == 'EXIT':
            return "GOODBYE"
        
        # Reverse the string as a simple transformation
        return f"Python received: '{text}' (reversed: '{text[::-1]}')"

def stdio_mode():
    """Handle standard input/output communication"""
    print("[Python] Running in stdio mode", flush=True)
    
    try:
        mp = MessageProcessor()
        while True:
            # Read from stdin
            line = sys.stdin.readline()
            if not line:
                break
                
            line = line.strip()
            if not line:
                continue
                
            print(f"[Python] Received: {line}", flush=True)
            
            # Process the message
            response = mp.process_text(line)
            
            # Send response
            print(response, flush=True)
            
            if line.upper() == 'EXIT':
                print("[Python] Exiting stdio mode", flush=True)
                break
                
    except KeyboardInterrupt:
        print("[Python] Interrupted", flush=True)
    except Exception as e:
        print(f"[Python] Error: {e}", flush=True)

def json_mode():
    """Handle JSON over stdio communication"""
    print("[Python] Running in JSON mode", flush=True)
    
    try:
        mp = MessageProcessor()
        while True:
            # Read JSON from stdin
            line = sys.stdin.readline()
            if not line:
                break
                
            line = line.strip()
            if not line:
                continue
                
            try:
                # Parse JSON
                data = json.loads(line)
                print(f"[Python] Received JSON: ID={data.get('Id')}, Message={data.get('Message')}", flush=True)
                
                # Process message
                response = mp.process_message(data)
                
                # Send JSON response
                print(json.dumps(response), flush=True)
                
                # Check for exit signal
                if data.get('Id') == -1 or data.get('Message') == 'EXIT':
                    print("[Python] Exiting JSON mode", flush=True)
                    break
                    
            except json.JSONDecodeError:
                print(f"[Python] Invalid JSON received: {line}", flush=True)
                
    except KeyboardInterrupt:
        print("[Python] Interrupted", flush=True)
    except Exception as e:
        print(f"[Python] Error: {e}", flush=True)

def named_pipe_mode(pipe_name: str):
    """Handle named pipe communication"""
    if not WINDOWS:
        print("[Python] Named pipes are only supported on Windows", flush=True)
        return
    
    print(f"[Python] Running in named pipe mode with pipe: {pipe_name}", flush=True)
    
    try:
        # Create named pipe
        pipe = win32pipe.CreateNamedPipe(
            f'\\\\.\\pipe\\{pipe_name}',
            win32pipe.PIPE_ACCESS_DUPLEX,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536, 0, None
        )
        
        print("[Python] Waiting for C# connection...", flush=True)
        
        # Wait for connection
        win32pipe.ConnectNamedPipe(pipe, None)
        print("[Python] Connected to C#", flush=True)
        
        try:
            mp = MessageProcessor()
            while True:
                # Read message length (4 bytes)
                length_bytes = win32file.ReadFile(pipe, 4)[1]
                if len(length_bytes) != 4:
                    break
                    
                message_length = struct.unpack('<I', length_bytes)[0]
                
                # Read message
                message_bytes = win32file.ReadFile(pipe, message_length)[1]
                message_json = message_bytes.decode('utf-8')
                
                # Parse JSON
                data = json.loads(message_json)
                print(f"[Python] Received via pipe: ID={data.get('Id')}, Message={data.get('Message')}", flush=True)
                
                # Process message
                response = mp.process_message(data)
                response_json = json.dumps(response)
                response_bytes = response_json.encode('utf-8')
                
                # Send response length and data
                win32file.WriteFile(pipe, struct.pack('<I', len(response_bytes)))
                win32file.WriteFile(pipe, response_bytes)
                
                # Check for exit
                if data.get('Id') == -1 or data.get('Message') == 'EXIT':
                    print("[Python] Exiting pipe mode", flush=True)
                    break
                    
        finally:
            # Clean up
            win32pipe.DisconnectNamedPipe(pipe)
            win32file.CloseHandle(pipe)
            
    except pywintypes.error as e:
        print(f"[Python] Pipe error: {e}", flush=True)
    except Exception as e:
        print(f"[Python] Error: {e}", flush=True)

def main():
    """Main entry point based on command line arguments"""
    if len(sys.argv) < 2:
        print("Usage: python python_script.py <mode> [pipe_name]")
        print("Modes: stdio, json, pipe")
        return
    
    while True:
       json_mode()
    

if __name__ == "__main__":
    main()
