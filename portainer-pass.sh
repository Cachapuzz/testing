#!/usr/bin/env bash
    
if [ -z "\$1" ]; then
    echo -e "\\nPlease call '\$0 <password>' to run this command!\\n"
    exit 1
fi
    
htpasswd -nb -B admin \$1 | cut -d ":" -f 2