#!/bin/bash

# Setup Swap File for 3GB VPS
# This script creates a 2GB swap file to help with memory management

set -e

SWAP_SIZE="2G"
SWAP_FILE="/swapfile"

echo "🔧 Setting up swap file for 3GB VPS..."

# Check if swap already exists
if [ -f $SWAP_FILE ]; then
    echo "⚠️  Swap file already exists. Checking size..."
    CURRENT_SIZE=$(du -h $SWAP_FILE | cut -f1)
    echo "Current swap size: $CURRENT_SIZE"

    if [ "$CURRENT_SIZE" != "2.0G" ]; then
        echo "🔄 Resizing swap file..."
        sudo swapoff $SWAP_FILE
        sudo rm $SWAP_FILE
    else
        echo "✅ Swap file already properly configured."
        exit 0
    fi
fi

# Create swap file
echo "📁 Creating ${SWAP_SIZE} swap file..."
sudo fallocate -l $SWAP_SIZE $SWAP_FILE

# Set permissions
echo "🔒 Setting swap file permissions..."
sudo chmod 600 $SWAP_FILE

# Make it a swap file
echo "💾 Setting up swap space..."
sudo mkswap $SWAP_FILE

# Enable swap
echo "🔄 Enabling swap..."
sudo swapon $SWAP_FILE

# Make it permanent
echo "⚙️  Making swap permanent..."
if ! grep -q "$SWAP_FILE" /etc/fstab; then
    echo "$SWAP_FILE none swap sw 0 0" | sudo tee -a /etc/fstab
fi

# Configure swappiness for server workload
echo "🎛️  Configuring swappiness..."
echo 'vm.swappiness=10' | sudo tee -a /etc/sysctl.conf
echo 'vm.vfs_cache_pressure=50' | sudo tee -a /etc/sysctl.conf

# Apply immediately
sudo sysctl vm.swappiness=10
sudo sysctl vm.vfs_cache_pressure=50

# Show current memory status
echo "📊 Current memory status:"
free -h
echo ""
echo "📊 Swap status:"
swapon --show

echo "✅ Swap configuration completed!"
echo "💡 System now has extra 2GB virtual memory for peak usage."