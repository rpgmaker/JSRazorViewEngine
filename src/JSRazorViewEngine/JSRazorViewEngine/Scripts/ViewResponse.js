 var ViewResponse = function () {
            return {
                Buffer: [],
                Clear: function () { this.Buffer.length = 0; },
                Write: function (data) { this.Buffer.push(data); },
                GetBuffer: function () { 
                    var content = jQuery('<view>');
                    var plains = [];
                    for(var i = 0; i < this.Buffer.length; i++){
                        var item = this.Buffer[i];
                        if(item instanceof jQuery){
                            if(plains.length > 0){
                                content.append(plains.join(''));
                                plains.length = 0;
                            }
                            content.append(item);
                        }else{
                              plains.push(item);
                        }
                    }
                    if(plains.length > 0){
                        content.append(plains.join(''));
                        plains.length = 0;
                    }
                    return content.contents(); 
                },
                SetBuffer: function (data) { this.Buffer = data; }
            }
        };