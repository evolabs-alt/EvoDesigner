# EvoDesigner Licensing System Design

## Overview
This document outlines the licensing system for EvoDesigner to control deployment and track installations across different machines.

## Licensing Models

### Option 1: Simple Serial Key Based (Recommended for Start)
- Each license has a unique serial key
- Key is validated against machine hardware ID
- Offline activation support
- Easy to implement and manage

### Option 2: Online Activation
- License key validated with online server
- Real-time activation/deactivation
- Usage tracking and analytics
- Requires internet connection

### Option 3: Hardware-Locked License
- License tied to specific hardware components
- Most secure but less flexible
- No transfer between machines without deactivation

### Option 4: Floating License (Network License)
- Limited number of concurrent users
- License server manages checkouts
- Best for organizations

## Recommended Implementation: Hybrid Approach

Combine hardware-locked serial keys with optional online validation for flexibility.

## Architecture

### Components:

1. **License Key Generator** (Your side)
   - Generates unique license keys
   - Associates keys with customer information
   - Manages license database

2. **License Validator** (In Application)
   - Validates license on startup
   - Checks hardware ID match
   - Handles grace periods and trials

3. **License Storage** (User's Machine)
   - Encrypted license file in user's AppData
   - Contains: Key, Hardware ID, Activation Date, Expiry

4. **License Server** (Optional - Online validation)
   - REST API for online activation
   - License usage tracking
   - Remote deactivation support

## License Key Format

### Recommended Format:
```
XXXX-XXXX-XXXX-XXXX-XXXX
```

Where each segment encodes:
- Product identifier
- License type (Trial, Standard, Professional, Enterprise)
- Expiry date (or perpetual)
- Checksum for validation

### Example:
```
ED11-ST01-A5F3-B2C8-7D4E
│    │    │              └─ Checksum
│    │    └─ Random data + hardware binding
│    └─ License type (ST=Standard, PR=Professional, EN=Enterprise)
└─ Product (ED11 = EvoDesigner 1.1)
```

## Hardware Fingerprinting

Collect and hash:
- CPU ID
- Motherboard Serial Number
- MAC Address (primary network adapter)
- Windows Product ID

**Algorithm:**
```csharp
string machineId = SHA256(CPU_ID + MB_SERIAL + MAC_ADDRESS + WINDOWS_ID)
                   .Substring(0, 16); // First 16 chars
```

## Activation Flow

### First Launch (No License):
```
1. Application starts
2. Check for license file
3. No license found → Show activation dialog
4. User enters license key
5. Validate key format and checksum
6. Generate hardware fingerprint
7. Bind license to hardware
8. (Optional) Online validation with server
9. Save encrypted license file
10. Allow application to run
```

### Subsequent Launches:
```
1. Application starts
2. Load license file
3. Decrypt and validate
4. Check hardware fingerprint match
5. Check expiry date (if applicable)
6. (Optional) Periodic online check (every 30 days)
7. Allow application to run
```

## License Types

### 1. Trial License
- Duration: 30 days
- Full features
- No activation required
- Auto-generated on first launch

### 2. Standard License
- Perpetual (no expiry)
- Single machine activation
- Can transfer to new machine (deactivate old)
- Basic features

### 3. Professional License
- Perpetual
- 2 machine activations
- Advanced features (scheduler, data binding)
- Priority support

### 4. Enterprise License
- Perpetual
- 5+ machine activations
- All features
- Custom branding option
- Premium support

## Grace Period & Offline Support

### Grace Period:
- 30 days after expiry for renewals
- Application shows warning but continues to work
- Prevents sudden disruption

### Offline Activation:
1. User enters license key offline
2. App generates "activation request code" (hardware ID + key)
3. User sends code to support
4. Support generates "activation response code"
5. User enters response code
6. App validates and activates

## License File Structure

**Location:** `%APPDATA%\EvoDesigner\license.dat`

**Contents (Encrypted):**
```json
{
  "LicenseKey": "ED11-ST01-A5F3-B2C8-7D4E",
  "LicenseType": "Standard",
  "MachineId": "A1B2C3D4E5F6G7H8",
  "ActivationDate": "2025-10-11",
  "ExpiryDate": null,
  "CustomerEmail": "user@company.com",
  "MaxActivations": 1,
  "Features": ["BasicDesign", "Printing", "DataBinding"],
  "Signature": "SHA256_HASH_OF_ALL_FIELDS"
}
```

**Encryption:** AES-256 with embedded key

## License Management Portal (Optional)

Web portal where customers can:
- View their licenses
- See activated machines
- Deactivate licenses remotely
- Transfer licenses
- Purchase additional licenses
- Download license files

## Pricing Tiers Example

| Feature | Trial | Standard | Professional | Enterprise |
|---------|-------|----------|--------------|------------|
| Price | Free | $99 | $299 | $999+ |
| Duration | 30 days | Perpetual | Perpetual | Perpetual |
| Machines | 1 | 1 | 2 | 5+ |
| Basic Design | ✓ | ✓ | ✓ | ✓ |
| Printing | ✓ | ✓ | ✓ | ✓ |
| Data Binding | ✓ | ✓ | ✓ | ✓ |
| Scheduled Printing | ✗ | ✗ | ✓ | ✓ |
| Update Queries | ✗ | ✗ | ✓ | ✓ |
| Multi-Printer Routing | ✗ | ✗ | ✓ | ✓ |
| API Access | ✗ | ✗ | ✗ | ✓ |
| Custom Branding | ✗ | ✗ | ✗ | ✓ |
| Support | Community | Email | Priority | 24/7 |

## Implementation Steps

### Phase 1: Basic License Validation (Week 1)
1. Create License model classes
2. Implement hardware fingerprinting
3. Create license validator
4. Add activation dialog
5. Implement license file storage
6. Update app to check license on startup

### Phase 2: License Key Generation (Week 1-2)
1. Create key generator tool
2. Implement key format and checksum
3. Create simple license database (SQLite or CSV)
4. Build key generator UI

### Phase 3: Online Validation (Week 2-3)
1. Build REST API for license server
2. Implement activation endpoints
3. Add deactivation support
4. Create usage tracking

### Phase 4: Management Portal (Week 3-4)
1. Build customer portal
2. Add license management features
3. Integrate payment system (Stripe/PayPal)
4. Add reporting and analytics

## Security Considerations

### Prevent Piracy:
1. **Obfuscate Code** - Use .NET obfuscator (ConfuserEx, Dotfuscator)
2. **Encrypt License File** - AES-256 encryption
3. **Anti-Tampering** - Checksum validation
4. **Online Validation** - Periodic server checks
5. **Hardware Binding** - Tie to specific machine

### Handle Cracks:
1. Regular updates with new protection
2. Online license validation
3. Usage analytics to detect suspicious patterns
4. DMCA takedown notices for pirated copies

## User Experience

### Keep It Simple:
- Trial works immediately (no key needed)
- Single-field license entry
- Clear error messages
- Easy license transfer process
- Offline activation option

### Avoid Frustration:
- Grace period for expired licenses
- Allow one machine change per year
- Don't lock out users abruptly
- Provide clear support contact

## Support Scenarios

### Lost License Key:
- Customer portal shows license keys
- Email retrieval option
- Support can look up by email

### Machine Replacement:
- Deactivate old machine in portal
- Activate new machine
- Or automatic detection of hardware change

### Multiple Machines (Professional/Enterprise):
- Track activation count
- Allow up to limit
- Provide activation/deactivation tools

## Legal Considerations

### EULA (End User License Agreement):
- Include with installer
- Must accept before first use
- Covers usage terms, restrictions
- Single vs multi-machine rights

### Privacy:
- Collect minimal data
- Disclose what's collected (hardware ID, usage stats)
- GDPR compliance if EU users

## Alternative: Subscription Model

Consider modern SaaS approach:
- Monthly/Annual subscription
- Always latest version
- Cloud features included
- Easier revenue prediction
- Lower entry price

Example:
- $9.99/month - Standard
- $24.99/month - Professional  
- $49.99/month - Enterprise

## Tools & Libraries

### .NET Libraries:
- **Portable.Licensing** - Open source licensing library
- **Standard.Licensing** - Another option
- **Microsoft Software Licensing** - Enterprise-grade

### License Server:
- ASP.NET Core Web API
- Azure Functions (serverless)
- AWS Lambda

### Payment Processing:
- Stripe
- PayPal
- Paddle (handles VAT/taxes)

### Obfuscation:
- Dotfuscator (Microsoft)
- ConfuserEx (Free, open source)
- SmartAssembly (RedGate)

## Quick Start: Minimum Viable License System

For fastest implementation:

1. **Hardcode Trial Period**
   - 30 days from first install
   - Store install date in registry/AppData

2. **Simple Serial Key**
   - Fixed format validation
   - No hardware binding (trust-based)
   - Manual activation tracking

3. **Email-Based Support**
   - Users email for license key
   - You manually send unique key
   - They enter in app

**Time to Implement:** 2-3 days

## Recommended Approach for EvoDesigner

Given your current state, I recommend:

### Start Simple:
1. Implement trial mode (30 days)
2. Add license key entry dialog
3. Validate key format and checksum
4. Bind to hardware ID
5. Store encrypted license file

### Expand Later:
1. Add online activation API
2. Build customer portal
3. Implement payment integration
4. Add subscription model

### Timeline:
- **Week 1:** Basic licensing (trial + key validation)
- **Week 2:** Hardware binding and license storage
- **Week 3:** Online validation and server
- **Week 4:** Customer portal and payment

## Next Steps

Would you like me to:
1. ✅ **Implement basic licensing system** (trial + serial key validation)
2. ✅ **Create license key generator tool**
3. ✅ **Add activation dialog to application**
4. ✅ **Build online validation API** (optional)
5. ✅ **Create customer license portal** (optional)

Let me know which approach you prefer, and I'll start implementing!
