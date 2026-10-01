#!/usr/bin/env python3
import sys
import json
import requests
import time

class UnityMCPClient:
    def __init__(self, host='localhost', port=8090):
        self.host = host
        self.port = port
        self.base_url = f'http://{host}:{port}'
        
    def send_request(self, method, params=None):
        """Send a request to the Unity MCP server"""
        try:
            # Unity MCP expects requests in this format
            request_data = {
                'id': str(int(time.time() * 1000)),
                'type': 'request',
                'method': method,
                'params': params or {}
            }
            
            response = requests.post(
                self.base_url,
                json=request_data,
                headers={'Content-Type': 'application/json'},
                timeout=30
            )
            
            if response.status_code == 200:
                return response.json()
            else:
                return {
                    'status': 'error',
                    'error': f'HTTP {response.status_code}: {response.text}'
                }
                
        except requests.exceptions.ConnectionError:
            return {
                'status': 'error',
                'error': 'Connection refused. Is the Unity Editor running with the MCP Bridge?'
            }
        except requests.exceptions.Timeout:
            return {
                'status': 'error',
                'error': 'Request timed out'
            }
        except Exception as e:
            return {
                'status': 'error',
                'error': str(e)
            }

def main():
    if len(sys.argv) < 2:
        print("Usage: python3 unity_mcp_client.py <command> [params]")
        print("Example: python3 unity_mcp_client.py project.analyze")
        print("Example: python3 unity_mcp_client.py ping")
        sys.exit(1)
    
    command = sys.argv[1]
    params = {}
    
    # Parse additional parameters if provided
    if len(sys.argv) > 2:
        try:
            params = json.loads(sys.argv[2])
        except json.JSONDecodeError:
            print("Invalid JSON parameters")
            sys.exit(1)
    
    client = UnityMCPClient()
    
    print(f"Sending command: {command}")
    print(f"Parameters: {json.dumps(params, indent=2) if params else 'None'}")
    print(f"Connecting to Unity Editor at {client.base_url}...")
    print()
    
    result = client.send_request(command, params)
    print(json.dumps(result, indent=2))

if __name__ == "__main__":
    main() 