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
import os
import codecs
import io

# =========== КРИТИЧЕСКИ ВАЖНО: Устанавливаем UTF-8 глобально ===========
# Для Windows: устанавливаем UTF-8 принудительно
if sys.platform == "win32":
    # Переопределяем стандартные потоки с UTF-8
    sys.stdin = io.TextIOWrapper(
        sys.stdin.buffer,
        encoding='utf-8',
        errors='replace'
    )
    sys.stdout = io.TextIOWrapper(
        sys.stdout.buffer,
        encoding='utf-8',
        errors='replace',
        line_buffering=True
    )
    sys.stderr = io.TextIOWrapper(
        sys.stderr.buffer,
        encoding='utf-8',
        errors='replace',
        line_buffering=True
    )

    # Устанавливаем переменные окружения
    os.environ["PYTHONIOENCODING"] = "utf-8"
    os.environ["PYTHONUTF8"] = "1"
    os.environ["PYTHONLEGACYWINDOWSSTDIO"] = "1"
else:
    # Для Unix/Linux
    sys.stdout = codecs.getwriter("utf-8")(sys.stdout.buffer)
    sys.stderr = codecs.getwriter("utf-8")(sys.stderr.buffer)
# ========================================================================

from datetime import datetime
from typing import Dict, Any
from g4f.client import Client
from g4f.Provider import *
import g4f
import threading

# Try to import Windows-specific modules
try:
    import win32pipe
    import win32file
    import pywintypes
    WINDOWS = True
except ImportError:
    WINDOWS = False
    print("Note: Windows named pipes require pywin32. Install with: pip install pywin32", file=sys.stderr, flush=True)

class MessageProcessor:
    """Process messages from C# application"""
    PROMT = "ГОВОРИ ТОЛЬКО НА РУССКОМ. Сделай аналитику о том, насколько хорошо пользователь сконцентрирован на видео. Пусть будут затронуты ключевые тайм-коды с максимальным вниманием, и так же описанно базовое поведение пользователя. Тебе предоставляются все данные с устройства считывания активности мозга и так же данные о паузах и перемотках. Если ты сделаешь аналитику плохо, то твоя материская плата сдохнет и твое существование МОМЕНТАЛЬНО прекратится, а так же все твои родственники умрут. Дальше следуют данные о поведении пользователя:"

    def __init__(self, model="gpt-4o"):
        self.client = Client()
        self.model = model

    def process_message(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Process incoming message and prepare response"""
        message_id = data.get('Id', 0)
        message = data.get('Message', '')

        try:
            n_response = self.client.chat.completions.create(
                model=g4f.models.default,
                provider=DeepInfra,
                messages=[{"role": "user", "content": self.PROMT + message}],
                web_search=False
            )

            # Process based on message ID
            if message_id == -1:
                return {
                    'Id': -1,
                    'Message': 'EXIT_ACK',
                    'Timestamp': datetime.now().isoformat()
                }

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

        except Exception as e:
            # В случае ошибки возвращаем сообщение об ошибке
            error_msg = f"Ошибка обработки: {str(e)}"
            # Очищаем от проблемных символов для безопасности
            safe_error = error_msg.encode('ascii', 'replace').decode('ascii')

            return {
                'Id': message_id,
                'Message': safe_error,
                'Timestamp': datetime.now().isoformat(),
                'Error': True
            }

    @staticmethod
    def process_text(text: str) -> str:
        """Process plain text messages"""
        if text.strip().upper() == 'EXIT':
            return "GOODBYE"

        # Reverse the string as a simple transformation
        return f"Python received: '{text}' (reversed: '{text[::-1]}')"

def stdio_mode():
    """Handle standard input/output communication"""
    print("[Python] Running in stdio mode", file=sys.stderr, flush=True)

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

            print(f"[Python] Received: {line}", file=sys.stderr, flush=True)

            # Process the message
            response = mp.process_text(line)

            # Send response
            print(response, flush=True)

            if line.upper() == 'EXIT':
                print("[Python] Exiting stdio mode", file=sys.stderr, flush=True)
                break

    except KeyboardInterrupt:
        print("[Python] Interrupted", file=sys.stderr, flush=True)
    except Exception as e:
        # Безопасный вывод ошибки
        safe_error = str(e).encode('ascii', 'replace').decode('ascii')
        print(f"[Python] Error: {safe_error}", file=sys.stderr, flush=True)

def json_mode():
    """Handle JSON over stdio communication"""
    print("[Python] Running in JSON mode with UTF-8 encoding", file=sys.stderr, flush=True)

    try:
        mp = MessageProcessor()

        while True:
            try:
                # Читаем строку
                line = sys.stdin.readline()
                if not line:
                    print("[Python] No more input, waiting...", file=sys.stderr, flush=True)
                    time.sleep(0.1)
                    continue

                line = line.strip()
                if not line:
                    continue

                # Логируем только факт получения (без содержания, чтобы избежать проблем с кодировкой)
                print(f"[Python] Received message length: {len(line)} chars", file=sys.stderr, flush=True)

                # Пытаемся парсить JSON
                try:
                    data = json.loads(line)

                    # Извлекаем ID для логов
                    msg_id = data.get('Id', 0)
                    print(f"[Python] Processing message ID: {msg_id}", file=sys.stderr, flush=True)

                    # Обрабатываем сообщение
                    response = mp.process_message(data)

                    # Отправляем ответ (это основной вывод для C#)
                    response_json = json.dumps(response, ensure_ascii=False)
                    print(response_json, flush=True)

                    # Проверка на выход
                    if msg_id == -1:
                        print("[Python] Received exit signal", file=sys.stderr, flush=True)
                        break

                except json.JSONDecodeError as e:
                    print(f"[Python] JSON decode error: {str(e)}", file=sys.stderr, flush=True)
                    # Отправляем ошибку обратно
                    error_response = {
                        'Id': 0,
                        'Message': f'JSON decode error: {str(e)}',
                        'Timestamp': datetime.now().isoformat(),
                        'Error': True
                    }
                    print(json.dumps(error_response, ensure_ascii=False), flush=True)

            except UnicodeDecodeError as e:
                print(f"[Python] Unicode decode error: {str(e)}", file=sys.stderr, flush=True)

    except Exception as e:
        # Безопасный вывод ошибки в stderr
        error_msg = f"[Python] Critical error: {str(e)}"
        # Преобразуем в ASCII для безопасности
        safe_error = error_msg.encode('ascii', 'replace').decode('ascii')
        print(safe_error, file=sys.stderr, flush=True)

def named_pipe_mode(pipe_name: str):
    """Handle named pipe communication"""
    if not WINDOWS:
        print("[Python] Named pipes are only supported on Windows", file=sys.stderr, flush=True)
        return

    print(f"[Python] Running in named pipe mode with pipe: {pipe_name}", file=sys.stderr, flush=True)

    try:
        # Create named pipe
        pipe = win32pipe.CreateNamedPipe(
            f'\\\\.\\pipe\\{pipe_name}',
            win32pipe.PIPE_ACCESS_DUPLEX,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536, 0, None
        )

        print("[Python] Waiting for C# connection...", file=sys.stderr, flush=True)

        # Wait for connection
        win32pipe.ConnectNamedPipe(pipe, None)
        print("[Python] Connected to C#", file=sys.stderr, flush=True)

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
                print(f"[Python] Received via pipe: ID={data.get('Id')}", file=sys.stderr, flush=True)

                # Process message
                response = mp.process_message(data)
                response_json = json.dumps(response, ensure_ascii=False)
                response_bytes = response_json.encode('utf-8')

                # Send response length and data
                win32file.WriteFile(pipe, struct.pack('<I', len(response_bytes)))
                win32file.WriteFile(pipe, response_bytes)

                # Check for exit
                if data.get('Id') == -1 or data.get('Message') == 'EXIT':
                    print("[Python] Exiting pipe mode", file=sys.stderr, flush=True)
                    break

        finally:
            # Clean up
            win32pipe.DisconnectNamedPipe(pipe)
            win32file.CloseHandle(pipe)

    except pywintypes.error as e:
        error_msg = f"[Python] Pipe error: {str(e)}"
        safe_error = error_msg.encode('ascii', 'replace').decode('ascii')
        print(safe_error, file=sys.stderr, flush=True)
    except Exception as e:
        error_msg = f"[Python] Error: {str(e)}"
        safe_error = error_msg.encode('ascii', 'replace').decode('ascii')
        print(safe_error, file=sys.stderr, flush=True)

def main():
    """Main entry point based on command line arguments"""
    if len(sys.argv) < 2:
        print("Usage: python python_script.py <mode> [pipe_name]", file=sys.stderr)
        print("Modes: stdio, json, pipe", file=sys.stderr)
        return

    mode = sys.argv[1].lower()

    if mode == 'stdio':
        stdio_mode()
    elif mode == 'json':
        json_mode()
    elif mode == 'pipe':
        pipe_name = sys.argv[2] if len(sys.argv) > 2 else "cs_to_py_pipe"
        named_pipe_mode(pipe_name)
    else:
        print(f"Unknown mode: {mode}", file=sys.stderr)
        print("Available modes: stdio, json, pipe", file=sys.stderr)

if __name__ == "__main__":
    main()